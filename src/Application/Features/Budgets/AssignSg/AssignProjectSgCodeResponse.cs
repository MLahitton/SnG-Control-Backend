using SnG.Control.Application.Features.Projects;
using SnG.Control.Application.Features.ProjectTasks;

namespace SnG.Control.Application.Features.Budgets.AssignSg;

public sealed record AssignProjectSgCodeResponse(ProjectResponse Project, ProjectTaskResponse PrepareQuotationTask);
