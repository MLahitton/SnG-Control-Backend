using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SnG.Control.Application.Features.CommercialQuotations;
using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Application.Files;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Infrastructure.Persistence.Configurations;

namespace SnG.Control.Infrastructure.Persistence.Stores;

public sealed class EfCommercialQuotationStore(SnGControlDbContext context, IFileStorage storage) : ICommercialQuotationStore
{
    public Task<CommercialQuotationState?> LoadAsync(Guid projectId, CancellationToken token) => ReadAsync(projectId, false, token);

    private async Task<CommercialQuotationState?> ReadAsync(Guid id, bool locked, CancellationToken token)
    {
        var projects = locked ? context.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {id} FOR UPDATE") : context.Projects.Where(p => p.Id == id);
        var project = await projects.AsNoTracking().SingleOrDefaultAsync(token);
        if (project is null) return null;
        var tasks = locked ? context.ProjectTasks.FromSqlInterpolated($"SELECT * FROM \"ProjectTasks\" WHERE \"ProjectId\" = {id} FOR UPDATE") : context.ProjectTasks.AsNoTracking().Where(t => t.ProjectId == id);
        var versions = locked ? context.BudgetQuotationVersions.FromSqlInterpolated($"SELECT * FROM \"BudgetQuotationVersions\" WHERE \"ProjectId\" = {id} AND \"VersionNumber\" = 1 FOR UPDATE")
            : context.BudgetQuotationVersions.AsNoTracking().Where(v => v.ProjectId == id && v.VersionNumber == 1);
        var taskList = await tasks.ToArrayAsync(token); var version = await versions.SingleOrDefaultAsync(token);
        var catalog = version is null ? null : await context.CommercialCatalogs.AsNoTracking().SingleOrDefaultAsync(c => c.BudgetQuotationVersionId == version.Id, token);
        var items = catalog is null ? [] : await context.CommercialCatalogItems.AsNoTracking().Where(i => i.CommercialCatalogId == catalog.Id).ToArrayAsync(token);
        var ids = items.Select(i => i.Id).ToArray();
        var extractions = await context.CommercialCatalogItemExtractions.AsNoTracking().Where(e => ids.Contains(e.CatalogItemId)).ToArrayAsync(token);
        var quotation = await context.CommercialQuotations.AsNoTracking().SingleOrDefaultAsync(q => q.ProjectId == id, token);
        return new(project, taskList, version, catalog, items, extractions, quotation);
    }

    public async Task<CommercialQuotationConfirmationResult> ConfirmAsync(CommercialQuotationState expected, string headSubject,
        CommercialQuotation quotation, IReadOnlyList<CommercialQuotationItem> items, byte[] bytes, CommercialQuotationPreviewResponse preview, CancellationToken token)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(token);
        var state = await ReadAsync(expected.Project.Id, true, token);
        if (state is null) return new(CommercialQuotationStatus.ProjectNotFound);
        var status = state.Validate(headSubject, true);
        if (status != CommercialQuotationStatus.Success) return new(status);
        if (state.Version!.Id != expected.Version!.Id || state.Catalog!.Id != expected.Catalog!.Id
            || state.Version.ConfirmedFileStorageKey != expected.Version.ConfirmedFileStorageKey
            || state.Items.Count != expected.Items.Count || state.Items.Any(i => !expected.Items.Any(e => e.Id == i.Id && e.Section == i.Section
                && e.SourceRow == i.SourceRow && CommercialTechnicalDifferences.Values(e) == CommercialTechnicalDifferences.Values(i))))
            return new(CommercialQuotationStatus.StructuralMismatch);
        return await PersistAsync(state, quotation, items, bytes, preview, transaction, token);
    }

    internal async Task<CommercialQuotationConfirmationResult> PersistAsync(CommercialQuotationState state, CommercialQuotation quotation,
        IReadOnlyList<CommercialQuotationItem> items, byte[] bytes, CommercialQuotationPreviewResponse preview, IDbContextTransaction transaction, CancellationToken token)
    {
        var version = state.Version!; var temporaryKey = version.ConfirmedFileStorageKey!;
        byte[] temporaryBytes;
        try { temporaryBytes = await storage.ReadAsync(temporaryKey, token); }
        catch (FileNotFoundException) { return new(CommercialQuotationStatus.DocumentUnavailable); }
        catch (DirectoryNotFoundException) { return new(CommercialQuotationStatus.DocumentUnavailable); }
        var review = state.Tasks.Single(t => t.Type == ProjectTaskType.ReviewQuotation);
        context.Attach(review); context.Attach(version);
        var reviewBefore = context.Entry(review).CurrentValues.Clone(); var versionBefore = context.Entry(version).CurrentValues.Clone();
        var present = new ProjectTask(state.Project.Id, OperationalArea.Commercial, ProjectTaskType.PresentQuotationToClient, state.Project.CreatedByGoogleSubject!);
        var saved = false; var deleteAttempted = false; var commitStarted = false;
        try
        {
            review.Complete(); present.Start();
            foreach (var (task, property) in new[] { (review, nameof(ProjectTask.CompletedAt)), (present, nameof(ProjectTask.AssignedAt)), (present, nameof(ProjectTask.StartedAt)) })
            {
                var time = (DateTime)context.Entry(task).Property(property).CurrentValue!;
                context.Entry(task).Property(property).CurrentValue = new DateTime(time.Ticks - time.Ticks % 10, DateTimeKind.Utc);
            }
            version.ClearConfirmedFileStorageKey();
            context.Add(quotation); context.AddRange(items); context.Add(present);
            await context.SaveChangesAsync(token);
            await storage.SaveAsync(quotation.FileStorageKey, bytes, token); saved = true;
            deleteAttempted = true; await storage.DeleteAsync(temporaryKey, token);
            commitStarted = true; await transaction.CommitAsync(token);
            return new(CommercialQuotationStatus.Success, new(state.Project.Id, quotation.Id, quotation.ProjectTotal, true,
                Response(review), Response(present), preview.HasTechnicalDifferences, preview.TechnicalDifferencesCount));
        }
        catch (Exception failure)
        {
            var errors = new List<Exception>(); var rolledBack = false;
            try { await transaction.RollbackAsync(CancellationToken.None); rolledBack = true; }
            catch (Exception rollbackFailure) { errors.Add(rollbackFailure); }
            // A lost COMMIT acknowledgement is not evidence of rollback. Keep the final file in that case.
            if (rolledBack || !commitStarted)
            {
                if (deleteAttempted)
                    try { await RestoreTemporaryAsync(temporaryKey, temporaryBytes); }
                    catch (Exception restoreFailure) { errors.Add(restoreFailure); }
                if (saved)
                    try { await storage.DeleteAsync(quotation.FileStorageKey, CancellationToken.None); }
                    catch (Exception cleanupFailure) { errors.Add(cleanupFailure); }
            }
            context.Entry(review).CurrentValues.SetValues(reviewBefore); context.Entry(version).CurrentValues.SetValues(versionBefore);
            foreach (var entity in items.Cast<object>().Concat([quotation, present, review, version])) context.Entry(entity).State = EntityState.Detached;
            if (errors.Count > 0) throw new AggregateException("Commercial confirmation failed; file/database state requires inspection.", new[] { failure }.Concat(errors));
            if (failure is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
                && postgres.ConstraintName is CommercialQuotationConfiguration.ProjectIndexName or ProjectTaskConfiguration.PresentQuotationIndexName)
                return new(CommercialQuotationStatus.AlreadyConfirmed);
            if (failure is OperationCanceledException) throw;
            throw new InvalidOperationException("Commercial quotation could not be stored.", failure);
        }
    }

    private async Task RestoreTemporaryAsync(string key, byte[] bytes)
    {
        byte[]? existing = null;
        try { existing = await storage.ReadAsync(key, CancellationToken.None); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        if (existing is null) await storage.SaveAsync(key, bytes, CancellationToken.None);
        else if (!existing.AsSpan().SequenceEqual(bytes)) throw new IOException("Operational file changed during compensation.");
    }

    public async Task<CommercialQuotationFileState?> GetFileAsync(Guid id, CancellationToken token)
    {
        var project = await context.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, token);
        return project is null ? null : new(project, await context.CommercialQuotations.AsNoTracking().SingleOrDefaultAsync(q => q.ProjectId == id, token));
    }
    private static ProjectTaskResponse Response(ProjectTask task) => new(task.Id, task.ProjectId, task.Area, task.Type, task.Status,
        task.AssignedAt, task.StartedAt, task.CompletedAt, task.AssignedToGoogleSubject);
}
