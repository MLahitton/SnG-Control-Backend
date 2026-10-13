namespace SnG.Control.Application.Features.Budgets.PreviewQuotation;

public interface IBudgetQuotationParser
{
    Task<BudgetQuotationParseResult> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken);
}
