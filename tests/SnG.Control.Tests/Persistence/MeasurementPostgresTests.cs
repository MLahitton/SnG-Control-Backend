using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using SnG.Control.Application.Features.ClientQuotationDecisions;
using SnG.Control.Application.Features.Measurements;
using SnG.Control.Application.Features.Notifications;
using SnG.Control.Application.Features.ProjectDocuments;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Domain.ValueObjects;
using SnG.Control.Infrastructure.Files;
using SnG.Control.Infrastructure.Persistence;
using SnG.Control.Infrastructure.Persistence.Stores;
using SnG.Control.Tests.Fixtures;
using SnG.Control.Tests.TestDoubles;
namespace SnG.Control.Tests.Persistence;

public sealed class MeasurementPostgresTests
{
    [VerificationPostgresFact]
    public async Task PersistentChecklistConcurrentInitializationChangesSubmissionAndCompensation()
    {
        var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SNG_VERIFICATION_TEST_DATABASE"));
        Assert.Equal("127.0.0.1", config.Host); Assert.Equal("verification_test", config.Database);
        SnGControlDbContext Db(bool fail = false) => new(new DbContextOptionsBuilder<SnGControlDbContext>().UseNpgsql(config.ConnectionString).AddInterceptors(new FailSave(fail)).Options);
        EfMeasurementStore Store(SnGControlDbContext db) => new(db, TestTiming.Value, new(db, new ProjectNotificationPlanner(TestTiming.Value)));
        using var files = new TemporaryStorage(); var storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = files.Root }));
        EfProjectDocumentStore Documents(SnGControlDbContext db, string subject = "installer", SystemRole role = SystemRole.Installation) => new(db, storage, new(new IdentityDouble(subject), new ActiveRoleDouble(role), new IdentityDouble("budgets")), TestTiming.Value);
        var actor = new DecisionActor("installer", "installer@example.test", "Installer"); var now = TestTiming.Value.UtcNow;
        await using var db = Db(); await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
        var client = new Client("Measurement synthetic"); var project = new Project(client.Id, "Measurement synthetic", "City", ProductLine.Classic, "advisor");
        db.AddRange(client, project); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Error("MeasurementUnavailable", () => Store(db).InitializeAsync(project.Id, actor, default));
        var version = new BudgetQuotationVersion(project.Id, 1, "synthetic.xlsx", "Client", "Project", "City", "Line", "Line", "Author", "SG9001");
        var catalog = new CommercialCatalog(version.Id); var quote = new CommercialQuotation(project.Id, version.Id, catalog.Id, "synthetic.xlsx", 100, "head", null, null);
        var present = new ProjectTask(project.Id, OperationalArea.Commercial, ProjectTaskType.PresentQuotationToClient, "advisor", now.AddDays(-1)); present.Start(now.AddDays(-1), now.AddDays(1)); present.Complete(now);
        var decision = new ClientQuotationDecision(project.Id, quote.Id, present.Id, ClientQuotationDecisionType.Approved, null, now, "advisor", null, null);
        var accounting = new ProjectTask(project.Id, OperationalArea.Accounting, ProjectTaskType.RegisterInitialDeposit, "accountant", now); accounting.StartUntracked(now); accounting.Complete(now);
        var handoff = new AccountingHandoff(project.Id, decision.Id, quote.Id, 1, accounting.Id, now, "advisor", null, null);
        var task = new ProjectTask(project.Id, OperationalArea.Installation, ProjectTaskType.TakeMeasurements, "installer", now);
        db.AddRange(version, catalog, quote, present, decision, accounting, handoff, new InitialDeposit(handoff, 50, now, now, null, null, "accountant", null, null), task);
        foreach (var (quantity, index) in new[] { 6, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }.Select((q, i) => (q, i)))
        {
            var values = new CatalogItemValues((index + 1).ToString(), "Room", "Window", null, null, null, null, 1.2m, 2, 2.4m, quantity);
            var source = new CommercialCatalogItem(catalog.Id, CommercialItemSection.Main, index + 1, values);
            db.AddRange(source, new CommercialQuotationItem(quote.Id, source.Id, CommercialItemSection.Main, index + 1, values, 10, 100));
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Error("MeasurementAccessDenied", () => Store(db).InitializeAsync(project.Id, actor with { Subject = "other" }, default));
        await using (var failed = Db(true)) await Assert.ThrowsAsync<InvalidOperationException>(() => Store(failed).InitializeAsync(project.Id, actor, default));
        Assert.False(await db.MeasurementChecklists.AnyAsync(c => c.ProjectId == project.Id));
        await using (var a = Db()) await using (var b = Db()) await Task.WhenAll(Store(a).InitializeAsync(project.Id, actor, default), Store(b).InitializeAsync(project.Id, actor, default));
        var state = (await Store(db).ReadAsync(project.Id, new("installer", SystemRole.Installation), default))!;
        Assert.Equal(21, state.Summary.Initial); Assert.Equal(21, state.Structures.Count); Assert.Equal(21, state.Summary.Pending);
        Assert.True(state.CanModify); Assert.Equal("InProgress", state.TaskStatus);
        Assert.Null(await Store(db).ReadAsync(project.Id, new("other", SystemRole.Installation), default));
        Assert.Null(await Store(db).ReadAsync(project.Id, new("installer", SystemRole.Accounting), default));
        Assert.False((await Store(db).ReadAsync(project.Id, new("manager", SystemRole.Management), default))!.CanModify);
        async Task Refresh()
        {
            await using var reopened = Db();
            state = (await Store(reopened).ReadAsync(project.Id, new("installer", SystemRole.Installation), default))!;
        }
        async Task Execute(MeasurementCommand command) { await using var c = Db(); await Store(c).ExecuteAsync(project.Id, command, actor, default); await Refresh(); }
        MeasurementCommand Command(string action, Guid? id = null, bool? measured = null) => new(Guid.NewGuid(), state.Revision, action, id, measured);
        var quotationBefore = System.Text.Json.JsonSerializer.Serialize(await db.CommercialQuotationItems.AsNoTracking().Where(i => i.CommercialQuotationId == quote.Id).OrderBy(i => i.Id).ToArrayAsync());
        var first = state.Structures.First(s => s.UnitCount == 6); var mark = Command("Measure", first.Id, true);
        await using (var a = Db()) await using (var b = Db()) await Task.WhenAll(Store(a).ExecuteAsync(project.Id, mark, actor, default), Store(b).ExecuteAsync(project.Id, mark, actor, default));
        await Refresh(); Assert.NotNull(state.Structures.Single(s => s.Id == first.Id).MeasuredAt); Assert.Equal(1, state.Summary.Measured); Assert.Single(state.History, h => h.Action == "Measure");
        await Error("MeasurementRequestConflict", () => Store(db).ExecuteAsync(project.Id, mark with { Measured = false }, actor, default));
        await Error("MeasurementRevisionConflict", () => Store(db).ExecuteAsync(project.Id, mark with { RequestKey = Guid.NewGuid() }, actor, default));
        await Execute(Command("Measure", first.Id, false)); Assert.Equal(0, state.Summary.Measured); Assert.Null(state.Structures.Single(s => s.Id == first.Id).MeasuredAt); Assert.Contains(state.History, h => h.Action == "Unmeasure" && h.Actor.Name == "Installer");
        await Execute(new(Guid.NewGuid(), state.Revision, "Add", Description: "Onsite door", Width: 1.3m, Height: 2.1m, Note: "Synthetic site note"));
        var added = state.Structures.Single(s => s.ItemId is null); Assert.Null(added.MeasuredAt); Assert.Equal(22, state.Summary.Active);
        await Execute(Command("Retire", added.Id));
        var retire = Command("Retire", first.Id);
        await using (var a = Db()) await using (var b = Db())
        {
            async Task<string> Retire(SnGControlDbContext context, MeasurementCommand command)
            { try { await Store(context).ExecuteAsync(project.Id, command, actor, default); return "OK"; } catch (DocumentException e) { return e.Code; } }
            var outcomes = await Task.WhenAll(Retire(a, retire), Retire(b, retire with { RequestKey = Guid.NewGuid() }));
            Assert.Single(outcomes, o => o == "OK"); Assert.Single(outcomes, o => o == "MeasurementRevisionConflict");
        }
        await Refresh(); Assert.Equal(20, state.Summary.Active); Assert.Equal(20, state.Summary.Pending); Assert.Equal(0, state.Summary.Progress); Assert.Equal(2, state.Summary.Retired);
        Assert.Equal(22, state.Structures.Count); Assert.Equal(5, state.Structures.Count(s => s.ItemId == first.ItemId && s.RetiredAt is null));
        foreach (var retiredId in new[] { first.Id, added.Id })
        {
            Assert.NotNull(state.Structures.Single(s => s.Id == retiredId).RetiredAt);
            var audit = Assert.Single(state.History, h => h.Action == "Retire" && h.StructureId == retiredId);
            Assert.Equal("Installer", audit.Actor.Name); Assert.Equal(DateTimeKind.Utc, audit.At.Kind);
            await using var reopened = Db();
            var saved = await reopened.MeasurementStructures.AsNoTracking().SingleAsync(s => s.Id == retiredId);
            Assert.Equal("installer", saved.RetiredBySubject); Assert.Equal(audit.At, saved.RetiredAt);
        }
        await Error("MeasurementStructureRetired", () => Store(db).ExecuteAsync(project.Id, Command("Retire", added.Id), actor, default));
        await Error("MeasurementsIncomplete", () => Store(db).ExecuteAsync(project.Id, Command("Submit") with { DocumentIds = [Guid.NewGuid()] }, actor, default));
        foreach (var row in state.Structures.Where(s => s.RetiredAt is null).ToArray()) await Execute(Command("Measure", row.Id, true));
        Assert.Equal(100, state.Summary.Progress); Assert.Equal(0, state.Summary.Pending);
        await Error("MeasurementAccessDenied", () => Documents(db, role: SystemRole.Management).UploadMeasurementsAsync(project.Id, Uploads("no.pdf"), default));
        await Error("MeasurementAccessDenied", () => Documents(db, "other").UploadMeasurementsAsync(project.Id, Uploads("no.pdf"), default));
        await Error("InvalidDocumentBatch", () => Documents(db).UploadMeasurementsAsync(project.Id, Uploads("../bad.pdf"), default));
        await using (var failed = Db(true)) await Assert.ThrowsAsync<InvalidOperationException>(() => Documents(failed).UploadMeasurementsAsync(project.Id, Uploads("rollback.pdf"), default));
        Assert.Empty(Directory.GetFiles(files.Root, "*", SearchOption.AllDirectories));
        var uploadKey = Guid.NewGuid();
        var upload = await Documents(db).UploadMeasurementsAsync(project.Id, Uploads("measures.pdf", "photo.jpg"), default, uploadKey); await Refresh();
        var retryUpload = await Documents(db).UploadMeasurementsAsync(project.Id, Uploads("measures.pdf", "photo.jpg"), default, uploadKey);
        Assert.Equal(upload.Documents.Select(d => d.Id).Order(), retryUpload.Documents.Select(d => d.Id).Order());
        Assert.Equal(2, state.Documents.Count); var ids = upload.Documents.Select(d => d.Id).ToArray();
        await Error("InvalidReplacement", () => Documents(db, "advisor", SystemRole.CommercialAdvisor).UploadAsync(project.Id, Replacing(ids[0]), default));
        var submit = Command("Submit") with { DocumentIds = ids };
        await Error("MeasurementDocumentsChanged", () => Store(db).ExecuteAsync(project.Id, submit with { DocumentIds = [ids[0]] }, actor, default));
        await using (var failed = Db(true)) await Assert.ThrowsAsync<InvalidOperationException>(() => Store(failed).ExecuteAsync(project.Id, submit, actor, default));
        await Refresh(); Assert.Null(state.SubmissionId); Assert.Equal("InProgress", state.TaskStatus); Assert.Empty(await db.MeasurementSubmissionDocuments.Where(d => d.ProjectId == project.Id).ToArrayAsync());
        await using (var a = Db()) await using (var b = Db()) await Task.WhenAll(Store(a).ExecuteAsync(project.Id, submit, actor, default), Store(b).ExecuteAsync(project.Id, submit, actor, default));
        await Refresh(); Assert.NotNull(state.SubmissionId); Assert.Equal("Completed", state.TaskStatus); Assert.False(state.CanModify);
        Assert.Single(state.History, h => h.Action == "Submit"); Assert.Equal(22, state.Snapshot!.Structures.Count); Assert.Equal(ids.Order(), state.Snapshot.DocumentIds.Order());
        Assert.Equal(2, await db.MeasurementSubmissionDocuments.CountAsync(d => d.ProjectId == project.Id));
        await Error("MeasurementsLocked", () => Store(db).ExecuteAsync(project.Id, Command("Measure", first.Id, false), actor, default));
        await Error("MeasurementsLocked", () => Documents(db).UploadMeasurementsAsync(project.Id, Uploads("late.pdf"), default));
        await Error("MeasurementsLocked", () => Store(db).ExecuteAsync(project.Id, Command("Retire", state.Structures.First(s => s.RetiredAt is null).Id), actor, default));
        Assert.Equal(quotationBefore, System.Text.Json.JsonSerializer.Serialize(await db.CommercialQuotationItems.AsNoTracking().Where(i => i.CommercialQuotationId == quote.Id).OrderBy(i => i.Id).ToArrayAsync()));
        Assert.Equal(14, await db.CommercialQuotationItems.CountAsync(i => i.CommercialQuotationId == quote.Id)); Assert.Equal(21, await db.CommercialQuotationItems.Where(i => i.CommercialQuotationId == quote.Id).SumAsync(i => i.Quantity));
        Assert.Equal(3, await db.ProjectTasks.CountAsync(t => t.ProjectId == project.Id));
        Assert.Null((await db.ProjectTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id)).DeadlineAt);
        Assert.All(await db.MeasurementStructures.AsNoTracking().Where(s => s.ProjectId == project.Id && s.MeasuredAt != null).ToArrayAsync(), s => Assert.Equal("installer", s.MeasuredBySubject));
        Assert.Equal(2, Directory.GetFiles(files.Root, "*", SearchOption.AllDirectories).Length);
    }
    private static async Task Error(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<DocumentException>(action)).Code);
    private static async IAsyncEnumerable<DocumentUpload> Uploads(params string[] names)
    {
        for (var i = 0; i < names.Length; i++) { using var stream = new MemoryStream([1, 2, 3]); yield return new($"file-{i}", names[i], "application/octet-stream", null, stream); }
        await Task.CompletedTask;
    }
    private static async IAsyncEnumerable<DocumentUpload> Replacing(Guid id)
    { using var stream = new MemoryStream([1]); yield return new("file-0", "replace.pdf", null, id, stream); await Task.CompletedTask; }
    private sealed class FailSave(bool fail) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { if (fail) throw new InvalidOperationException("Synthetic pre-commit failure"); return ValueTask.FromResult(result); }
    }
}
