using System.Text.Json.Serialization;
using SnG.Control.Api.Contracts;
using SnG.Control.Api.Errors;
using SnG.Control.Application.Features.Clients;
using SnG.Control.Application.Features.Clients.Create;
using SnG.Control.Application.Features.Clients.GetById;
using SnG.Control.Application.Features.Clients.List;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Application.Features.Projects.Create;
using SnG.Control.Application.Features.Projects.GetById;
using SnG.Control.Application.Features.Projects.List;
using SnG.Control.Infrastructure.Persistence.Stores;

namespace SnG.Control.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new ProductLineJsonConverter());
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        });

        services.AddScoped<IClientStore, EfClientStore>();
        services.AddScoped<IProjectStore, EfProjectStore>();
        services.AddScoped<CreateClientUseCase>();
        services.AddScoped<GetClientByIdUseCase>();
        services.AddScoped<ListClientsUseCase>();
        services.AddScoped<CreateProjectUseCase>();
        services.AddScoped<GetProjectByIdUseCase>();
        services.AddScoped<ListProjectsUseCase>();
        return services;
    }
}
