using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnG.Control.Application.Access;
using SnG.Control.Application.Features.Accounting;
using SnG.Control.Application.Features.ClientQuotationDecisions;
using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Tests.TestDoubles;
namespace SnG.Control.Tests.Api;

public sealed class AccountingTests
{
    [Theory]
    [InlineData("other", "CommercialAdvisor", "send-approved-quotation-to-accounting", 201)]
    [InlineData("valid", "CommercialAdvisor", "send-approved-quotation-to-accounting", 403)]
    [InlineData("accounting", "Accounting", "send-approved-quotation-to-accounting", 403)]
    [InlineData("management", "Management", "send-approved-quotation-to-accounting", 403)]
    [InlineData("accounting", "Accounting", "initial-deposit", 201)]
    [InlineData("other", "CommercialAdvisor", "initial-deposit", 403)]
    [InlineData("valid", "CommercialHead", "initial-deposit", 403)]
    [InlineData("management", "Management", "initial-deposit", 403)]
    [InlineData("budgets", "Budgets", "initial-deposit", 403)]
    [InlineData("other", "Accounting", "initial-deposit", 403)]
    public async Task ActiveRoleAndAssignedSubjectAreRequired(string identity, string role, string path, int expected)
    {
        var store = new Store(); await using var fixture = Fixture(store); await fixture.InitializeAsync(); Identify(fixture, identity, role);
        using var result = await fixture.Http.PostAsJsonAsync($"/api/projects/{store.Id}/{path}", path == "initial-deposit" ? new { amount = "5000000.00", receivedAt = "2026-01-01T12:00:00-05:00", reference = "TEST", note = (string?)null } : (object)new { });
        Assert.Equal(expected, (int)result.StatusCode);
        if (expected == 201) { var json = await result.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(JsonValueKind.Null, json.GetProperty("task").GetProperty("quotationRound").ValueKind); }
        else Assert.Equal(0, store.Writes);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000000000000000.00")]
    [InlineData("0.001")]
    [InlineData("1e3")]
    [InlineData("1,50")]
    public async Task InvalidMoneyFailsBeforeStore(string amount)
    {
        var store = new Store(); await using var fixture = Fixture(store); await fixture.InitializeAsync(); Identify(fixture, "accounting", "Accounting");
        using var result = await fixture.Http.PostAsJsonAsync($"/api/projects/{store.Id}/initial-deposit", new { amount, receivedAt = "2026-01-01T12:00:00Z", reference = (string?)null, note = (string?)null });
        Assert.Equal(400, (int)result.StatusCode); Assert.Equal(0, store.Writes);
    }
    [Theory]
    [InlineData("2026-01-01T12:00:00")]
    [InlineData("2099-01-01T12:00:00Z")]
    [InlineData("invalid")]
    public async Task InvalidReceiptFailsBeforeStore(string receivedAt)
    {
        var store = new Store(); await using var fixture = Fixture(store); await fixture.InitializeAsync(); Identify(fixture, "accounting", "Accounting");
        using var result = await fixture.Http.PostAsJsonAsync($"/api/projects/{store.Id}/initial-deposit", new { amount = "1.00", receivedAt, reference = (string?)null, note = (string?)null });
        Assert.Equal(400, (int)result.StatusCode); Assert.Equal(0, store.Writes);
    }
    [Fact]
    public async Task MoneyUsesExistingRoundingAndCanonicalRead()
    {
        var store = new Store(); await using var fixture = Fixture(store); await fixture.InitializeAsync(); Identify(fixture, "accounting", "Accounting");
        using var result = await fixture.Http.PostAsJsonAsync($"/api/projects/{store.Id}/initial-deposit", new { amount = "1.005", receivedAt = "2026-01-01T12:00:00-05:00", reference = "TEST", note = (string?)null });
        Assert.Equal(201, (int)result.StatusCode); Assert.Equal(1.01m, store.Input!.Amount); Assert.Equal(17, store.Input.ReceivedAt.Hour);
        var body = await result.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("1.01", body.GetProperty("state").GetProperty("deposit").GetProperty("amount").GetString());
        Assert.Equal("UntrackedLegacy", body.GetProperty("task").GetProperty("leadTimeStatus").GetString());
    }
    [Theory]
    [InlineData("recordedAt")]
    [InlineData("subject")]
    [InlineData("currencyCode")]
    public async Task RejectsCallerAuthority(string field)
    {
        var store = new Store(); await using var fixture = Fixture(store); await fixture.InitializeAsync(); Identify(fixture, "accounting", "Accounting");
        using var result = await fixture.Http.PostAsJsonAsync($"/api/projects/{store.Id}/initial-deposit", new Dictionary<string, object?> { ["amount"] = "1.00", ["receivedAt"] = "2026-01-01T12:00:00Z", ["reference"] = null, ["note"] = null, [field] = "forbidden" });
        Assert.Equal(400, (int)result.StatusCode); Assert.Equal(0, store.Writes);
    }
    [Fact]
    public void UnscheduledTaskHasNoInventedSlaAndPreservesQuotationTiming()
    {
        var now = TestTiming.Value.UtcNow;
        var task = new ProjectTask(Guid.NewGuid(), OperationalArea.Accounting, ProjectTaskType.RegisterInitialDeposit, "accountant", now);
        Assert.Null(task.QuotationRound); Assert.Equal(SnG.Control.Application.Scheduling.LeadTimeStatus.NotStarted, TestTiming.Value.Status(task, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => TestTiming.Value.Start(task, now));
        task.StartUntracked(now); task.Complete(now);
        Assert.Equal(task.StartedAt, task.CompletedAt); Assert.Null(task.DeadlineAt); Assert.Null(task.WasOverdue);
        Assert.Equal(SnG.Control.Application.Scheduling.LeadTimeStatus.UntrackedLegacy, TestTiming.Value.Status(task, now));
        Assert.Throws<InvalidOperationException>(() => task.StartUntracked(now));
        Assert.Throws<InvalidOperationException>(() => new ProjectTask(Guid.NewGuid(), OperationalArea.Budgets, ProjectTaskType.PrepareQuotation).StartUntracked(now));
    }
    private static ApiFixture Fixture(Store store) => new() { ConfigureServices = s => { s.RemoveAll<IAccountingStore>(); s.AddSingleton<IAccountingStore>(store); } };
    private static void Identify(ApiFixture fixture, string identity, string role)
    {
        fixture.Http.DefaultRequestHeaders.Authorization = new(TestAuthenticationHandler.SchemeName, identity);
        fixture.Http.DefaultRequestHeaders.Remove("X-SnG-Active-Role"); fixture.Http.DefaultRequestHeaders.Add("X-SnG-Active-Role", role);
    }
    private sealed class Store : IAccountingStore
    {
        public Guid Id { get; } = Guid.NewGuid(); public int Writes { get; private set; }
        public DepositInput? Input { get; private set; }
        public Task<AccountingState?> ReadAsync(Guid id, ProjectReadScope scope, CancellationToken token) => Task.FromResult<AccountingState?>(AccountingState.From(id, null, null));
        public Task<AccountingResult> SendAsync(Guid id, DecisionActor actor, string accountant, CancellationToken token)
        {
            if (actor.Subject != "other-google-subject") return Task.FromResult(new AccountingResult(AccountingStatus.Forbidden));
            Writes++; return Task.FromResult(Result(id, accountant, null));
        }
        public Task<AccountingResult> RecordAsync(Guid id, DepositInput input, DecisionActor actor, CancellationToken token) { Writes++; Input = input; return Task.FromResult(Result(id, actor.Subject, input)); }
        private static AccountingResult Result(Guid id, string subject, DepositInput? input)
        {
            var now = TestTiming.Value.UtcNow; var task = new ProjectTask(id, OperationalArea.Accounting, ProjectTaskType.RegisterInitialDeposit, subject, now);
            var h = new AccountingHandoff(id, Guid.NewGuid(), Guid.NewGuid(), 2, task.Id, now, "advisor", null, null);
            InitialDeposit? d = null;
            if (input is not null) { task.StartUntracked(now); task.Complete(now); d = new(h, input.Amount, input.ReceivedAt, now, input.Reference, input.Note, subject, null, null); }
            return new(AccountingStatus.Success, new(AccountingState.From(id, h, d), ProjectTaskResponse.FromEntity(task, TestTiming.Value)));
        }
    }
}
