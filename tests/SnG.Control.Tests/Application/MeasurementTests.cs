using SnG.Control.Application.Access;
using SnG.Control.Application.Features.ClientQuotationDecisions;
using SnG.Control.Application.Features.Measurements;
using SnG.Control.Application.Features.ProjectDocuments;
using SnG.Control.Application.Identity;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Domain.ValueObjects;
using SnG.Control.Tests.TestDoubles;
namespace SnG.Control.Tests.Application;

public sealed class MeasurementTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData(new[] { 1 }, 1)]
    [InlineData(new[] { 6 }, 6)]
    [InlineData(new[] { 1, 6, 2 }, 9)]
    [InlineData(new[] { 6, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, 21)]
    public void ExpandsPersistentIndependentUnits(int[] quantities, int expected)
    {
        var checklist = Checklist();
        var rows = quantities.SelectMany((quantity, index) => {
            var item = new CommercialQuotationItem(checklist.CommercialQuotationId, Guid.NewGuid(), (CommercialItemSection)1, index + 1,
                new CatalogItemValues((index + 1).ToString(), "Room", "Window", null, null, null, null, 1.2m, 2m, 2.4m, quantity), 10, 100);
            return Enumerable.Range(1, quantity).Select(unit => MeasurementStructure.Quoted(checklist, item, unit, Now, "installer"));
        }).ToArray();
        Assert.Equal(expected, rows.Length); Assert.Equal(expected, rows.Select(r => r.Id).Distinct().Count());
        Assert.All(rows, r => { Assert.Null(r.MeasuredAt); Assert.Null(r.RetiredAt); Assert.Equal(checklist.ProjectId, r.ProjectId); });
        rows[0].Measure(true, Now, "installer"); Assert.Equal(1, MeasurementSummary.From(rows).Measured);
        rows[0].Measure(false, Now, "installer"); Assert.Equal(0, MeasurementSummary.From(rows).Measured);
    }
    [Fact]
    public void AddedRetiredAndZeroActiveHaveCorrectProgress()
    {
        var checklist = Checklist(); var row = MeasurementStructure.Added(checklist, "Door", 1.01m, 2m, "Site note", Now, "installer");
        Assert.Equal("Site note", row.Note); Assert.Equal(1.01m, row.Width); Assert.Null(row.MeasuredAt);
        row.Measure(true, Now, "installer"); Assert.Equal("installer", row.MeasuredBySubject);
        row.Retire(Now, "installer"); Assert.Equal("installer", row.RetiredBySubject);
        Assert.Equal(new MeasurementSummary(0, 1, 1, 0, 0, 0, 0), MeasurementSummary.From([row]));
        Assert.Throws<InvalidOperationException>(() => row.Retire(Now, "installer"));
        Assert.Throws<InvalidOperationException>(() => row.Measure(false, Now, "installer"));
        Assert.Throws<ArgumentException>(() => MeasurementStructure.Added(checklist, "Door", 0, 1, null, Now, "installer"));
        checklist.Submit(Now, "installer", "{}"); Assert.Throws<InvalidOperationException>(() => checklist.Advance());
        Assert.Throws<InvalidOperationException>(() => MeasurementStructure.Added(checklist, "Door", 1, 1, null, Now, "installer"));
    }
    [Theory]
    [InlineData(SystemRole.CommercialAdvisor)] [InlineData(SystemRole.Accounting)] [InlineData(SystemRole.Management)] [InlineData(SystemRole.CommercialHead)] [InlineData(SystemRole.Engineering)]
    public async Task SameSubjectOtherActiveRoleCannotWrite(SystemRole role)
    {
        var service = new MeasurementService(new UnusedStore(), new IdentityDouble("installer"), new ActiveRoleDouble(role), new Installer());
        Assert.Equal(403, (await Assert.ThrowsAsync<DocumentException>(() => service.InitializeAsync(Guid.NewGuid(), default))).Status);
        await Assert.ThrowsAsync<DocumentException>(() => service.ExecuteAsync(Guid.NewGuid(), new(Guid.NewGuid(), 0, "Submit", DocumentIds: [Guid.NewGuid()]), default));
    }
    [Fact]
    public async Task WrongConfiguredSubjectAndMalformedCommandAreRejected()
    {
        var service = new MeasurementService(new UnusedStore(), new IdentityDouble("other"), new ActiveRoleDouble(SystemRole.Installation), new Installer());
        await Assert.ThrowsAsync<DocumentException>(() => service.InitializeAsync(Guid.NewGuid(), default));
        service = new(new UnusedStore(), new IdentityDouble("installer"), new ActiveRoleDouble(SystemRole.Installation), new Installer());
        foreach (var command in new[] { new MeasurementCommand(Guid.Empty, 0, "Measure"), new(Guid.NewGuid(), 0, "Add", Width: -1, Height: 1, Description: "Door"), new(Guid.NewGuid(), 0, "Submit", DocumentIds: []), new(Guid.NewGuid(), 0, "Retire", StructureId: Guid.NewGuid(), Measured: true) })
            Assert.Equal("InvalidMeasurementCommand", (await Assert.ThrowsAsync<DocumentException>(() => service.ExecuteAsync(Guid.NewGuid(), command, default))).Code);
    }
    private static MeasurementChecklist Checklist() => new(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), Now, "installer");
    private sealed class Installer : IInstallationAssigneeIdentity { public string Subject => "installer"; }
    private sealed class UnusedStore : IMeasurementStore
    {
        public Task<MeasurementState?> ReadAsync(Guid id, ProjectReadScope scope, CancellationToken token) => throw new NotImplementedException();
        public Task InitializeAsync(Guid id, DecisionActor actor, CancellationToken token) => throw new NotImplementedException();
        public Task ExecuteAsync(Guid id, MeasurementCommand command, DecisionActor actor, CancellationToken token) => throw new NotImplementedException();
    }
}
