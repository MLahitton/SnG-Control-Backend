using SnG.Control.Application.Features.ProjectDocuments;
using SnG.Control.Application.Scheduling;
using SnG.Control.Api.Authorization;
using SnG.Control.Application.Features.CommercialQuotations;
using SnG.Control.Domain.Entities;
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
    public HttpClient BudgetsHttp { get; private set; } = null!;
    public BudgetsWorkflowStoreDouble BudgetsWorkflow { get; private set; } = null!;
    public ClientStoreDouble Clients { get; } = new();
    public ProjectStoreDouble Projects { get; } = new();
    public ProjectTaskStoreDouble Tasks { get; } = new();
    public DocumentDatabaseDouble Documents { get; private set; } = null!;
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    public NotificationStoreDouble Notifications { get; private set; } = null!;
    private readonly TemporaryStorage storage = new();
    public QuotationConfirmationStoreDouble Confirmations { get; private set; } = null!;

    public CommercialHandoffScenario Handoff { get; set; } = new();
    public CommercialQuotationScenario? Commercial { get; set; }

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
        builder.Services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
        builder.Services.AddSingleton(Clock);
        Tasks.Timing = new(Clock, new());
        Notifications = new(Projects, Tasks);
        Documents = new(Projects, Tasks);
        builder.Services.RemoveAll<IProjectDocumentStore>();
        builder.Services.AddScoped<IProjectDocumentStore>(provider => Documents.Store(provider.GetRequiredService<IStreamingFileStorage>(), provider.GetRequiredService<ProjectDocumentAccess>(), provider.GetRequiredService<TaskTiming>()));
        builder.Services.RemoveAll<SnG.Control.Application.Features.Notifications.IProjectNotificationStore>();
        builder.Services.AddSingleton<SnG.Control.Application.Features.Notifications.IProjectNotificationStore>(Notifications);
        builder.Services.Configure<BudgetsAssigneeOptions>(options => options.BudgetsAssigneeSubject = "budgets-test-subject");
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
        builder.Services.AddScoped<IFileStorage>(_ => new StorageProxy(Commercial is null ? Handoff : Commercial));
        builder.Services.RemoveAll<ICommercialQuotationStore>();
        builder.Services.AddScoped<ICommercialQuotationStore>(_ => new CommercialStoreProxy(Commercial!));
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
        BudgetsWorkflow = new(Projects, Tasks);
        builder.Services.AddSingleton<IBudgetsWorkflowStore>(BudgetsWorkflow);
        app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Http = new HttpClient { BaseAddress = new Uri(address) };
        BudgetsHttp = new HttpClient { BaseAddress = new Uri(address) };
        BudgetsHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName, "budgets");
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName, "valid");
    }

    public async Task DisposeAsync()
    {
        Http.Dispose();
        BudgetsHttp.Dispose();
        await app.DisposeAsync();
        storage.Dispose();
        Commercial?.Dispose();
    }

    // Scopes must not dispose the fixture-owned temporary storage after each HTTP request.
    private sealed class StorageProxy(IFileStorage inner) : IFileStorage
    {
        public Task<byte[]> ReadAsync(string key, CancellationToken token) => inner.ReadAsync(key, token);
        public Task SaveAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken token) => inner.SaveAsync(key, bytes, token);
        public Task DeleteAsync(string key, CancellationToken token) => inner.DeleteAsync(key, token);
    }
    private sealed class CommercialStoreProxy(ICommercialQuotationStore inner) : ICommercialQuotationStore
    {
        public Task<CommercialQuotationState?> LoadAsync(Guid id, CancellationToken token) => inner.LoadAsync(id, token);
        public Task<CommercialQuotationFileState?> GetFileAsync(Guid id, CancellationToken token) => inner.GetFileAsync(id, token);
        public Task<CommercialQuotationConfirmationResult> ConfirmAsync(CommercialQuotationState state, string head, CommercialQuotation quotation,
            IReadOnlyList<CommercialQuotationItem> items, byte[] bytes, CommercialQuotationPreviewResponse preview, CancellationToken token)
            => inner.ConfirmAsync(state, head, quotation, items, bytes, preview, token);
    }
}
