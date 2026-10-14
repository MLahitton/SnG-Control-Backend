using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SnG.Control.Application.Features.Budgets.ConfirmQuotation;
using SnG.Control.Application.Files;
using SnG.Control.Infrastructure.Files;
using SnG.Control.Api.Contracts;
using SnG.Control.Api.Errors;
using SnG.Control.Application.Features.Budgets;
using SnG.Control.Application.Features.Budgets.AssignSg;
using SnG.Control.Application.Features.Budgets.PreviewQuotation;
using SnG.Control.Infrastructure.Quotations;
using SnG.Control.Application.Features.Clients;
using SnG.Control.Application.Features.Clients.Create;
using SnG.Control.Application.Features.Clients.GetById;
using SnG.Control.Application.Features.Clients.List;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Application.Features.Projects.Create;
using SnG.Control.Application.Features.Projects.GetById;
using SnG.Control.Application.Features.Projects.List;
using SnG.Control.Infrastructure.Persistence.Stores;
using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Application.Features.ProjectTasks.SendToBudgets;
using SnG.Control.Application.Features.ProjectTasks.ListByArea;
using SnG.Control.Application.Features.ProjectTasks.ListByProject;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();
        services.AddSingleton<IValidateOptions<FileStorageOptions>, FileStorageOptionsValidator>();
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName).ValidateOnStart();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IBudgetQuotationConfirmationStore, EfBudgetQuotationConfirmationStore>();
        services.AddScoped<ConfirmBudgetQuotationUseCase>();
        services.AddOptions<BudgetQuotationParserOptions>().BindConfiguration(BudgetQuotationParserOptions.SectionName)
            .Validate(options => options.MaxUploadBytes > 0 && options.MaxUncompressedBytes > 0 && options.MaxZipEntries > 0,
                "Quotation preview technical limits must be positive.").ValidateOnStart();
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new ProductLineJsonConverter());
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<OperationalArea>(allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<ProjectTaskType>(allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<ProjectTaskStatus>(allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(new CommercialItemSectionJsonConverter());
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<CatalogItemImageStatus>(allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<BudgetQuotationWarningCode>(allowIntegerValues: false));
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        });

        services.AddScoped<IClientStore, EfClientStore>();
        services.AddScoped<IProjectStore, EfProjectStore>();
        services.AddScoped<IProjectTaskStore, EfProjectTaskStore>();
        services.AddScoped<IBudgetsWorkflowStore, EfBudgetsWorkflowStore>();
        services.AddScoped<AssignProjectSgCodeUseCase>();
        services.AddScoped<IBudgetQuotationParser, XlsxBudgetQuotationParser>();
        services.AddScoped<PreviewBudgetQuotationUseCase>();
        services.AddScoped<SendProjectToBudgetsUseCase>();
        services.AddScoped<ListProjectTasksByAreaUseCase>();
        services.AddScoped<ListProjectTasksByProjectUseCase>();
        services.AddScoped<CreateClientUseCase>();
        services.AddScoped<GetClientByIdUseCase>();
        services.AddScoped<ListClientsUseCase>();
        services.AddScoped<CreateProjectUseCase>();
        services.AddScoped<GetProjectByIdUseCase>();
        services.AddScoped<ListProjectsUseCase>();
        return services;
    }
}
