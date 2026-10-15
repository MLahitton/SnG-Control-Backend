using SnG.Control.Application.Identity;
using SnG.Control.Application.Scheduling;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.ProjectTasks.SendToBudgets;

public class SendProjectToBudgetsUseCase(IProjectStore projects, IProjectTaskStore tasks, IBudgetsAssigneeIdentity assignee, TaskTiming timing)
{
    public async Task<SendProjectToBudgetsResult> ExecuteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await projects.GetByIdAsync(projectId, cancellationToken) is null)
        {
            return new SendProjectToBudgetsResult.ProjectNotFound();
        }
        var task = new ProjectTask(projectId, OperationalArea.Budgets, ProjectTaskType.PrepareQuotation, assignee.Subject, timing.UtcNow);
        if (!await tasks.TryAddPrepareQuotationAsync(task, cancellationToken))
        {
            return new SendProjectToBudgetsResult.AlreadySent();
        }

        return new SendProjectToBudgetsResult.Created(ProjectTaskResponse.FromEntity(task, timing));
    }
}
