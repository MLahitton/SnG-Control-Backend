namespace SnG.Control.Application.Features.Budgets.ConfirmQuotation;

public interface IBudgetQuotationConfirmationStore
{
    Task<QuotationConfirmationStoreResult> ConfirmAsync(BudgetQuotationConfirmation confirmation, CancellationToken cancellationToken);
}
