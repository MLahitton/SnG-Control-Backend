namespace SnG.Control.Application.Features.Budgets.AssignSg;

public abstract record AssignProjectSgCodeResult
{
    private AssignProjectSgCodeResult() { }

    public sealed record Success(AssignProjectSgCodeResponse Response) : AssignProjectSgCodeResult;
    public sealed record ProjectNotFound : AssignProjectSgCodeResult;
    public sealed record ProjectNotSentToBudgets : AssignProjectSgCodeResult;
    public sealed record InvalidWorkflowState : AssignProjectSgCodeResult;
    public sealed record SgCodeAlreadyInUse : AssignProjectSgCodeResult;
}
