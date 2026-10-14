using SnG.Control.Api.Authorization;
using Microsoft.AspNetCore.Builder;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using SnG.Control.Api.Authentication;
using SnG.Control.Application.Features.Budgets.CommercialHandoff;
using SnG.Control.Application.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SnG.Control.Api;
using SnG.Control.Api.Controllers;
using SnG.Control.Application.Features.Clients;
using SnG.Control.Application.Features.Budgets;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Tests.TestDoubles;
using SnG.Control.Application.Features.Budgets.ConfirmQuotation;
using SnG.Control.Infrastructure.Files;
using SnG.Control.Tests.Fixtures;

namespace SnG.Control.Tests.Api;

public class ApiFixture : IAsyncLifetime
{
    private WebApplication app = null!;
    public HttpClient Http { get; private set; } = null!;
    public ClientStoreDouble Clients { get; } = new();
    public ProjectStoreDouble Projects { get; } = new();
    public ProjectTaskStoreDouble Tasks { get; } = new();
    private readonly TemporaryStorage storage = new();
    public QuotationConfirmationStoreDouble Confirmations { get; private set; } = null!;

    public CommercialHandoffScenario Handoff { get; set; } = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ClientsController).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddApiServices();
        builder.Services.Configure<CommercialHeadOptions>(options => options.CommercialHeadSubject = "google-test-subject");
        builder.Services.Configure<GoogleAuthenticationOptions>(options => options.ClientId = "test-client-id");
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
            options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
        }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
        builder.Services.RemoveAll<IBudgetQuotationCommercialHandoffStore>();
        builder.Services.RemoveAll<IFileStorage>();
        builder.Services.AddScoped<IBudgetQuotationCommercialHandoffStore>(_ => Handoff);
        builder.Services.AddScoped<IFileStorage>(_ => Handoff);
        builder.Services.Configure<FileStorageOptions>(options => options.RootPath = storage.Root);
        builder.Services.RemoveAll<IBudgetQuotationConfirmationStore>();
        Confirmations = new QuotationConfirmationStoreDouble(Projects, Tasks);
        builder.Services.AddSingleton<IBudgetQuotationConfirmationStore>(Confirmations);
        builder.Services.RemoveAll<IClientStore>();
        builder.Services.RemoveAll<IProjectStore>();
        builder.Services.RemoveAll<IProjectTaskStore>();
        builder.Services.RemoveAll<IBudgetsWorkflowStore>();
        builder.Services.AddSingleton<IClientStore>(Clients);
        builder.Services.AddSingleton<IProjectStore>(Projects);
        builder.Services.AddSingleton<IProjectTaskStore>(Tasks);
        builder.Services.AddSingleton<IBudgetsWorkflowStore>(new BudgetsWorkflowStoreDouble(Projects, Tasks));
        app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Http = new HttpClient { BaseAddress = new Uri(address) };
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName, "valid");
    }

    public async Task DisposeAsync()
    {
        Http.Dispose();
        await app.DisposeAsync();
        storage.Dispose();
    }
}
