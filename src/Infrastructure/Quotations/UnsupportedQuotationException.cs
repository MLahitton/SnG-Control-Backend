using SnG.Control.Application.Features.Budgets.PreviewQuotation;

namespace SnG.Control.Infrastructure.Quotations;

internal sealed class UnsupportedQuotationException(BudgetQuotationFileError error = BudgetQuotationFileError.UnsupportedWorkbookFormat) : Exception
{
    public BudgetQuotationFileError Error { get; } = error;
}
