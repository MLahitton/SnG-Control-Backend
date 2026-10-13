namespace SnG.Control.Application.Features.ProjectTasks.SendToBudgets;

public abstract record SendProjectToBudgetsResult
{
    private SendProjectToBudgetsResult() { }

    public sealed record Created(ProjectTaskResponse Task) : SendProjectToBudgetsResult;
    public sealed record ProjectNotFound : SendProjectToBudgetsResult;
    public sealed record AlreadySent : SendProjectToBudgetsResult;
}
