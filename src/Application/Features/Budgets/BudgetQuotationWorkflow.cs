using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.Budgets;

public static class BudgetQuotationWorkflow
{
    public static ProjectTask? ReadyTask(Project project, IEnumerable<ProjectTask> tasks)
    {
        var quotations = tasks.Where(task => task.Type == ProjectTaskType.PrepareQuotation).ToArray();
        if (project.SgCode is null || quotations.Length != 1) return null;
        var task = quotations[0];
        return task.Area == OperationalArea.Budgets && task.Status == ProjectTaskStatus.InProgress
            && task.StartedAt is not null && task.CompletedAt is null ? task : null;
    }
}
