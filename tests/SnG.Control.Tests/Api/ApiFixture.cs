using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SnG.Control.Api;
using SnG.Control.Api.Controllers;
using SnG.Control.Application.Features.Clients;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Tests.TestDoubles;

namespace SnG.Control.Tests.Api;

public class ApiFixture : IAsyncLifetime
{
    private WebApplication app = null!;
    public HttpClient Http { get; private set; } = null!;
    public ClientStoreDouble Clients { get; } = new();
    public ProjectStoreDouble Projects { get; } = new();
    public ProjectTaskStoreDouble Tasks { get; } = new();

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
        builder.Services.RemoveAll<IClientStore>();
        builder.Services.RemoveAll<IProjectStore>();
        builder.Services.RemoveAll<IProjectTaskStore>();
        builder.Services.AddSingleton<IClientStore>(Clients);
        builder.Services.AddSingleton<IProjectStore>(Projects);
        builder.Services.AddSingleton<IProjectTaskStore>(Tasks);
        app = builder.Build();
        app.UseExceptionHandler();
        app.MapControllers();
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Http = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        Http.Dispose();
        await app.DisposeAsync();
    }
}
