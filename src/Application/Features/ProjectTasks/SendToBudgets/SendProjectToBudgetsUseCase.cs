using SnG.Control.Application.Features.Projects;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.ProjectTasks.SendToBudgets;

public class SendProjectToBudgetsUseCase(IProjectStore projects, IProjectTaskStore tasks)
{
    public async Task<SendProjectToBudgetsResult> ExecuteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await projects.GetByIdAsync(projectId, cancellationToken) is null)
        {
            return new SendProjectToBudgetsResult.ProjectNotFound();
        }
        var task = new ProjectTask(projectId, OperationalArea.Budgets, ProjectTaskType.PrepareQuotation);
        if (!await tasks.TryAddPrepareQuotationAsync(task, cancellationToken))
        {
            return new SendProjectToBudgetsResult.AlreadySent();
        }

        return new SendProjectToBudgetsResult.Created(ProjectTaskResponse.FromEntity(task));
    }
}
