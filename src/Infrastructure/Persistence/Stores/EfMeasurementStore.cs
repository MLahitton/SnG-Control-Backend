using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SnG.Control.Application.Access;
using SnG.Control.Application.Features.ClientQuotationDecisions;
using SnG.Control.Application.Features.Measurements;
using SnG.Control.Application.Features.ProjectDocuments;
using SnG.Control.Application.Scheduling;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
namespace SnG.Control.Infrastructure.Persistence.Stores;

public sealed class EfMeasurementStore(SnGControlDbContext db, TaskTiming timing, EfProjectNotificationWriter notifications) : IMeasurementStore
{
    public async Task<MeasurementState?> ReadAsync(Guid id, ProjectReadScope scope, CancellationToken token)
    {
        if (scope.Role is not (SystemRole.Installation or SystemRole.Management)) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        if (!await db.Projects.AsNoTracking().Where(scope.Filter(db.ProjectTasks)).AnyAsync(p => p.Id == id, token)) return null;
        var tasks = await db.ProjectTasks.AsNoTracking().Where(t => t.ProjectId == id && t.Type == ProjectTaskType.TakeMeasurements).ToArrayAsync(token);
        if (tasks.Length != 1 || !await db.InitialDeposits.AnyAsync(d => d.ProjectId == id, token)) return null;
        var task = tasks[0];
        var checklist = await db.MeasurementChecklists.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == id, token);
        var rows = await db.MeasurementStructures.AsNoTracking().Where(x => x.ProjectId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.CommercialQuotationItemId).ThenBy(x => x.UnitOrdinal).ThenBy(x => x.Id).ToArrayAsync(token);
        var history = await db.MeasurementEvents.AsNoTracking().Where(x => x.ProjectId == id).OrderBy(x => x.Revision).ToArrayAsync(token);
        var documents = await db.ProjectDocuments.AsNoTracking().Where(d => d.ProjectId == id && d.IsMeasurementDocument && d.RemovedAt == null).OrderBy(d => d.UploadedAt).ThenBy(d => d.Id).ToArrayAsync(token);
        var views = await StructureViewsAsync(rows, token);
        await tx.CommitAsync(token);
        return new(id, task.Id, task.Status.ToString(), task.AssignedToGoogleSubject!,
            scope.Role == SystemRole.Installation && task.AssignedToGoogleSubject == scope.Subject && task.Status != ProjectTaskStatus.Completed && checklist?.SubmissionId is null,
            checklist?.Id, checklist?.Revision ?? 0, MeasurementSummary.From(rows), views,
            documents.Select(d => DocumentResponse.From(d, new HashSet<Guid>())).ToArray(),
            history.Select(e => new MeasurementHistory(e.Id, e.Action, e.StructureId, e.At, new(e.ActorEmail, e.ActorName), e.Revision, e.DetailsJson is null ? null : JsonSerializer.Deserialize<MeasurementDetailsChange>(e.DetailsJson))).ToArray(),
            checklist?.SubmissionId, checklist?.SubmittedAt, checklist?.SnapshotJson is { } json ? JsonSerializer.Deserialize<MeasurementSnapshot>(json) : null);
    }
    private async Task<MeasurementStructureView[]> StructureViewsAsync(MeasurementStructure[] rows, CancellationToken token)
    {
        var itemIds = rows.Where(s => s.CommercialQuotationItemId != null).Select(s => s.CommercialQuotationItemId!.Value).Distinct().ToArray();
        var images = await (from item in db.CommercialQuotationItems.AsNoTracking()
                            join extraction in db.CommercialCatalogItemExtractions.AsNoTracking() on item.CommercialCatalogItemId equals extraction.CatalogItemId
                            where itemIds.Contains(item.Id) && extraction.ImageStatus == CatalogItemImageStatus.Stored && extraction.ImageStorageKey != null && extraction.ImageContentType != null
                            select new { item.Id, extraction.CatalogItemId }).ToDictionaryAsync(x => x.Id, x => x.CatalogItemId, token);
        return rows.Select(s => MeasurementStructureView.From(s) with { ImageCatalogItemId = s.CommercialQuotationItemId is { } itemId && images.TryGetValue(itemId, out var imageId) ? imageId : null }).ToArray();
    }
    private async Task<ProjectTask> LockAsync(Guid id, string subject, CancellationToken token)
    {
        if (!await db.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {id} FOR UPDATE").AsNoTracking().AnyAsync(token)) throw new DocumentException(404, "ProjectNotFound");
        var tasks = await db.ProjectTasks.FromSqlInterpolated($"SELECT * FROM \"ProjectTasks\" WHERE \"ProjectId\" = {id} FOR UPDATE").AsNoTracking().ToArrayAsync(token);
        var candidates = tasks.Where(t => t.Type == ProjectTaskType.TakeMeasurements).ToArray();
        if (candidates.Length != 1 || !await db.InitialDeposits.AnyAsync(d => d.ProjectId == id, token)) throw new DocumentException(409, "MeasurementUnavailable");
        var task = candidates[0];
        if (task.AssignedToGoogleSubject != subject) throw new DocumentException(403, "MeasurementAccessDenied");
        return task;
    }
    public async Task InitializeAsync(Guid id, DecisionActor actor, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        var task = await LockAsync(id, actor.Subject, token);
        if (await db.MeasurementChecklists.AnyAsync(c => c.ProjectId == id, token)) return;
        if (task.Status != ProjectTaskStatus.Pending) throw new DocumentException(409, "MeasurementUnavailable");
        var deposit = await db.InitialDeposits.AsNoTracking().SingleAsync(d => d.ProjectId == id, token);
        var handoff = await db.AccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == deposit.AccountingHandoffId, token);
        var items = await db.CommercialQuotationItems.AsNoTracking().Where(i => i.CommercialQuotationId == deposit.CommercialQuotationId).ToArrayAsync(token);
        if (items.Length == 0 || items.Any(i => i.Quantity <= 0)) throw new DocumentException(409, "MeasurementCatalogInvalid");
        var now = timing.UtcNow;
        var checklist = new MeasurementChecklist(id, deposit.CommercialQuotationId, handoff.QuotationRound, task.Id, now, actor.Subject);
        var entities = new List<object> { checklist };
        foreach (var item in items)
            for (var unit = 0; unit < item.Quantity; unit++) entities.Add(MeasurementStructure.Quoted(checklist, item, checked(unit + 1), now, actor.Subject));
        entities.Add(new MeasurementEvent(checklist, Guid.NewGuid(), "", "Initialize", null, now, actor.Subject, actor.Email, actor.Name));
        db.Attach(task); var before = db.Entry(task).CurrentValues.Clone();
        EfProjectNotificationWriter.NotificationWriteSet? staged = null;
        try
        {
            task.StartUntracked(now); db.AddRange(entities); staged = await notifications.StageAsync([task], now, token);
            await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        }
        catch
        {
            staged?.RestoreTracking(); foreach (var entity in entities) db.Entry(entity).State = EntityState.Detached;
            db.Entry(task).CurrentValues.SetValues(before); db.Entry(task).State = EntityState.Detached; throw;
        }
    }
    public async Task ExecuteAsync(Guid id, MeasurementCommand command, DecisionActor actor, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        var task = await LockAsync(id, actor.Subject, token);
        var checklist = await db.MeasurementChecklists.AsNoTracking().SingleOrDefaultAsync(c => c.ProjectId == id, token) ?? throw new DocumentException(409, "MeasurementUnavailable");
        var normalized = command with { DocumentIds = command.DocumentIds?.Order().ToArray() };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized))));
        var replay = await db.MeasurementEvents.AsNoTracking().SingleOrDefaultAsync(e => e.ChecklistId == checklist.Id && e.RequestKey == command.RequestKey, token);
        if (replay is not null)
        {
            if (replay.Fingerprint != fingerprint || replay.ActorSubject != actor.Subject) throw new DocumentException(409, "MeasurementRequestConflict");
            return;
        }
        if (checklist.SubmissionId is not null || task.Status != ProjectTaskStatus.InProgress) throw new DocumentException(409, "MeasurementsLocked");
        if (checklist.Revision != command.ExpectedRevision) throw new DocumentException(409, "MeasurementRevisionConflict");
        var rows = await db.MeasurementStructures.AsNoTracking().Where(s => s.ChecklistId == checklist.Id).ToArrayAsync(token);
        var now = timing.UtcNow; var added = new List<object>(); var changed = new List<(object Entity, Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues Before)>();
        void Track(object entity) { db.Attach(entity); changed.Add((entity, db.Entry(entity).CurrentValues.Clone())); }
        EfProjectNotificationWriter.NotificationWriteSet? staged = null;
        try
        {
            Track(checklist); Guid? structureId = command.StructureId; MeasurementDetailsChange? details = null;
            if (command.Action == "Submit")
            {
                var summary = MeasurementSummary.From(rows);
                if (summary.Active == 0 || summary.Pending != 0) throw new DocumentException(409, "MeasurementsIncomplete");
                var docs = await db.ProjectDocuments.AsNoTracking().Where(d => d.ProjectId == id && d.IsMeasurementDocument && d.RemovedAt == null && d.ReplacedAt == null).ToArrayAsync(token);
                if (docs.Length == 0 || command.DocumentIds is null || !docs.Select(d => d.Id).Order().SequenceEqual(command.DocumentIds.Order())) throw new DocumentException(409, "MeasurementDocumentsChanged");
                checklist.Submit(now, actor.Subject, JsonSerializer.Serialize(new MeasurementSnapshot(checklist.CommercialQuotationId, checklist.QuotationRound, summary, await StructureViewsAsync(rows, token), docs.Select(d => d.Id).Order().ToArray())));
                added.AddRange(docs.Select(d => new MeasurementSubmissionDocument(checklist, d)));
                Track(task); task.Complete(now); staged = await notifications.StageAsync([task], now, token);
            }
            else
            {
                if (command.Action == "Add")
                {
                    var row = MeasurementStructure.Added(checklist, command.Description!, command.Width!.Value, command.Height!.Value, command.Note, now, actor.Subject);
                    row.RecordActual(command.Width.Value, command.Height.Value, command.Description!, now, actor.Subject);
                    details = new(new(null, null, null), MeasurementActualValues.From(row));
                    added.Add(row); structureId = row.Id;
                }
                else
                {
                    var row = rows.SingleOrDefault(r => r.Id == command.StructureId) ?? throw new DocumentException(404, "MeasurementStructureNotFound");
                    if (row.RetiredAt is not null) throw new DocumentException(409, "MeasurementStructureRetired");
                    Track(row);
                    if (command.Action == "RecordActual")
                    {
                        var before = MeasurementActualValues.From(row);
                        row.RecordActual(command.ActualWidth!.Value, command.ActualHeight!.Value, command.ActualSystemText!, now, actor.Subject);
                        details = new(before, MeasurementActualValues.From(row));
                    }
                    else if (command.Action == "Measure") row.Measure(command.Measured!.Value, now, actor.Subject);
                    else if (command.Action == "Retire") row.Retire(now, actor.Subject);
                    else throw new DocumentException(400, "InvalidMeasurementCommand");
                }
                checklist.Advance();
            }
            added.Add(new MeasurementEvent(checklist, command.RequestKey, fingerprint, command.Action == "Measure" && command.Measured == false ? "Unmeasure" : command.Action, structureId, now, actor.Subject, actor.Email, actor.Name, details is null ? null : JsonSerializer.Serialize(details)));
            db.AddRange(added); await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        }
        catch
        {
            staged?.RestoreTracking(); foreach (var entity in added) db.Entry(entity).State = EntityState.Detached;
            foreach (var (entity, before) in changed) { db.Entry(entity).CurrentValues.SetValues(before); db.Entry(entity).State = EntityState.Detached; }
            throw;
        }
    }
    public static async Task RequireUploadAsync(SnGControlDbContext db, Guid id, IReadOnlyList<ProjectTask> tasks, string subject, CancellationToken token)
    {
        var task = tasks.SingleOrDefault(t => t.Type == ProjectTaskType.TakeMeasurements);
        if (task is null || task.AssignedToGoogleSubject != subject) throw new DocumentException(403, "MeasurementAccessDenied");
        var checklist = await db.MeasurementChecklists.AsNoTracking().SingleOrDefaultAsync(c => c.ProjectId == id, token);
        if (checklist is null || checklist.SubmissionId is not null || task.Status != ProjectTaskStatus.InProgress) throw new DocumentException(409, "MeasurementsLocked");
    }
}
