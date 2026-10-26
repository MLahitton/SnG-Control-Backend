using System.Net.Http.Json;
using System.Text.Json;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Tests.Api;

public class LeadTimeContractTests
{
    [Fact]
    public async Task AllReadEndpoints_ExposeOnlyOfficialCamelCaseLeadTimeFields()
    {
        var clock = new ControlledTimeProvider(new(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc));
        var fixture = new ApiFixture { Clock = clock };
        await fixture.InitializeAsync();
        try
        {
            var client = new Client("Contract client"); fixture.Clients.Items.Add(client);
            var project = new Project(client.Id, "Contract project", "City", ProductLine.Classic, "google-test-subject");
            fixture.Projects.Items.Add(project);
            var task = new ProjectTask(project.Id, OperationalArea.Budgets, ProjectTaskType.PrepareQuotation,
                "budgets-test-subject", clock.Now);
            fixture.Tasks.Items.Add(task);
            fixture.Tasks.Timing.Start(task, clock.Now);

            foreach (var path in new[] { "/api/project-tasks?area=Budgets&status=InProgress", $"/api/projects/{project.Id}/tasks" })
            {
                var rows = await fixture.BudgetsHttp.GetFromJsonAsync<JsonElement>(path);
                AssertStatus(Assert.Single(rows.EnumerateArray()), "OnTrack");
            }

            clock.Now = task.DeadlineAt!.Value.AddSeconds(1);
            using var manager = new HttpClient { BaseAddress = fixture.Http.BaseAddress };
            manager.DefaultRequestHeaders.Authorization = new(TestAuthenticationHandler.SchemeName, "management");
            manager.DefaultRequestHeaders.Add("X-SnG-Active-Role", "Management");
            var tasks = await manager.GetFromJsonAsync<JsonElement>("/api/management/tasks");
            AssertStatus(Assert.Single(tasks.EnumerateArray()), "Overdue");
            var overview = await manager.GetFromJsonAsync<JsonElement>("/api/management/overview");
            Assert.Equal(1, overview.GetProperty("leadTime").GetProperty("overdue").GetInt32());
            Assert.False(overview.TryGetProperty("leakTime", out _));
            Assert.False(overview.TryGetProperty("LeadTime", out _));
            AssertStatus(Assert.Single(overview.GetProperty("attention").GetProperty("items").EnumerateArray()), "Overdue");
            Assert.False(task.WasOverdue); // Reading a projection must not persist timing state.
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static void AssertStatus(JsonElement task, string expected)
    {
        Assert.Equal(expected, task.GetProperty("leadTimeStatus").GetString());
        Assert.False(task.TryGetProperty("leakTimeStatus", out _));
        Assert.False(task.TryGetProperty("LeadTimeStatus", out _));
    }
}
