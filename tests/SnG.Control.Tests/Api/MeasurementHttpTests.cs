using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnG.Control.Api.Authorization;
using SnG.Control.Application.Access;
using SnG.Control.Application.Features.ClientQuotationDecisions;
using SnG.Control.Application.Features.Measurements;
using SnG.Control.Tests.TestDoubles;
namespace SnG.Control.Tests.Api;

public sealed class MeasurementHttpTests
{
    [Theory]
    [InlineData("valid", "Installation", 204)]
    [InlineData("valid", "CommercialAdvisor", 403)]
    [InlineData("valid", "CommercialHead", 403)]
    [InlineData("accounting", "Accounting", 403)]
    [InlineData("management", "Management", 403)]
    [InlineData("other", "Installation", 403)]
    public async Task DirectEndpointsEnforceOwnedActiveRole(string identity, string role, int expected)
    {
        var store = new Store(); await using var fixture = new ApiFixture { ConfigureServices = services => {
            services.RemoveAll<IMeasurementStore>(); services.AddSingleton<IMeasurementStore>(store);
            services.Configure<InstallationAssigneeOptions>(o => o.InstallationAssigneeSubject = "google-test-subject");
        } };
        await fixture.InitializeAsync();
        fixture.Http.DefaultRequestHeaders.Authorization = new(TestAuthenticationHandler.SchemeName, identity);
        fixture.Http.DefaultRequestHeaders.Remove("X-SnG-Active-Role"); fixture.Http.DefaultRequestHeaders.Add("X-SnG-Active-Role", role);
        using var initialize = await fixture.Http.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/measurements/initialize", new { });
        Assert.Equal(expected, (int)initialize.StatusCode);
        using var command = await fixture.Http.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/measurements/commands", new { requestKey = Guid.NewGuid(), expectedRevision = 0, action = "Measure", structureId = Guid.NewGuid(), measured = true });
        Assert.Equal(expected, (int)command.StatusCode); Assert.Equal(expected == 204 ? 2 : 0, store.Writes);
        using var malformed = await fixture.Http.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/measurements/commands", new { requestKey = Guid.NewGuid(), expectedRevision = 0, action = "Measure", actorSubject = "forged" });
        Assert.Equal(identity == "other" ? 403 : 400, (int)malformed.StatusCode);
    }
    private sealed class Store : IMeasurementStore
    {
        public int Writes { get; private set; }
        public Task<MeasurementState?> ReadAsync(Guid id, ProjectReadScope scope, CancellationToken token) => Task.FromResult<MeasurementState?>(null);
        public Task InitializeAsync(Guid id, DecisionActor actor, CancellationToken token) { Writes++; return Task.CompletedTask; }
        public Task ExecuteAsync(Guid id, MeasurementCommand command, DecisionActor actor, CancellationToken token) { Writes++; return Task.CompletedTask; }
    }
}
