using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnG.Control.Api.Authorization;
using SnG.Control.Application.Access;
using SnG.Control.Application.Features.ClientQuotationDecisions;
using SnG.Control.Application.Features.Engineering;
using SnG.Control.Application.Features.ProjectDocuments;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
namespace SnG.Control.Tests.Api;

public sealed class OpeningFixture : ApiFixture
{
    public OpeningDouble Store { get; } = new();
    public OpeningFixture() => ConfigureServices = services =>
    {
        services.Configure<EngineeringAssigneeOptions>(o => o.EngineeringAssigneeSubject = "google-test-subject");
        services.RemoveAll<IOpeningRecordStore>(); services.AddSingleton<IOpeningRecordStore>(Store);
    };
}
public sealed class OpeningRecordHttpTests(OpeningFixture fixture) : IClassFixture<OpeningFixture>
{
    [Theory]
    [InlineData("CommercialAdvisor", "Present", 204)]
    [InlineData("Engineering", "Present", 403)]
    [InlineData("Engineering", "ReviewCheck", 204)]
    [InlineData("CommercialAdvisor", "ReviewCheck", 403)]
    [InlineData("Management", "Accept", 403)]
    [InlineData(null, "Present", 403)]
    [InlineData("CommercialAdvisor", "Stop", 400)]
    public async Task OwnedActiveRoleAndClosedActionsAreRequired(string? role, string action, int status)
    {
        using var http = Client(role);
        var response = await http.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/opening-records/rounds/{Guid.NewGuid()}/commands", new OpeningCommand(Guid.NewGuid(), 0, action, Guid.NewGuid(), true));
        Assert.Equal(status, (int)response.StatusCode);
    }
    [Fact]
    public async Task InvalidChangeDescriptionsAndKeysDoNotReachPersistence()
    {
        using var http = Client("CommercialAdvisor"); var path = $"/api/projects/{Guid.NewGuid()}/opening-records/rounds/{Guid.NewGuid()}/commands";
        var before = fixture.Store.Calls;
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync(path, new OpeningCommand(Guid.Empty, 0, "Present"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync(path, new OpeningCommand(Guid.NewGuid(), 0, "RequestChanges", Changes: [new(Guid.NewGuid(), " ")]))).StatusCode);
        Assert.Equal(before, fixture.Store.Calls);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/opening-records/stop", new {})).StatusCode);
    }
    private HttpClient Client(string? role)
    {
        var http = new HttpClient { BaseAddress = fixture.Http.BaseAddress }; http.DefaultRequestHeaders.Authorization = new(TestAuthenticationHandler.SchemeName, "valid");
        if (role != null) http.DefaultRequestHeaders.Add(HttpActiveRoleContext.HeaderName, role); return http;
    }
}
public sealed class OpeningDouble : IOpeningRecordStore
{
    public int Calls;
    public Task<OpeningState?> ReadAsync(Guid id, ProjectReadScope scope, CancellationToken token) => Task.FromResult<OpeningState?>(null);
    public Task CommandAsync(Guid id, Guid roundId, OpeningCommand c, DecisionActor actor, SystemRole role, CancellationToken token) { Calls++; return Task.CompletedTask; }
    public Task UploadAsync(Guid id, Guid roundId, Guid key, long revision, OpeningDocumentKind kind, IAsyncEnumerable<DocumentUpload> files, DecisionActor actor, SystemRole role, CancellationToken token) => throw new NotSupportedException();
    public Task<DocumentDownload> DownloadAsync(Guid id, Guid documentId, ProjectReadScope scope, CancellationToken token) => throw new NotSupportedException();
    public Task InitializeHistoricalAsync(Guid id, Guid checklistId, string reason, DecisionActor actor, CancellationToken token) => throw new NotSupportedException();
}
