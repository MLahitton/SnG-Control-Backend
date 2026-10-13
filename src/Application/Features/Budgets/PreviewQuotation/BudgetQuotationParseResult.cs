namespace SnG.Control.Application.Features.Budgets.PreviewQuotation;

public abstract record BudgetQuotationParseResult
{
    private BudgetQuotationParseResult() { }
    public sealed record Success(BudgetQuotationDocument Document, IReadOnlyList<BudgetQuotationPreviewItem> Items,
        IReadOnlyList<BudgetQuotationWarning> Warnings) : BudgetQuotationParseResult;
    public sealed record Invalid(BudgetQuotationFileError Error) : BudgetQuotationParseResult;
}
