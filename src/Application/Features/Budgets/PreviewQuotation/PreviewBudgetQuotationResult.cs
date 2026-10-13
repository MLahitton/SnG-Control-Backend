namespace SnG.Control.Application.Features.Budgets.PreviewQuotation;

public abstract record PreviewBudgetQuotationResult
{
    private PreviewBudgetQuotationResult() { }
    public sealed record Success(BudgetQuotationPreviewResponse Response) : PreviewBudgetQuotationResult;
    public sealed record ProjectNotFound : PreviewBudgetQuotationResult;
    public sealed record InvalidWorkflowState : PreviewBudgetQuotationResult;
    public sealed record SgCodeMismatch : PreviewBudgetQuotationResult;
    public sealed record InvalidWorkbook(BudgetQuotationFileError Error) : PreviewBudgetQuotationResult;
}
