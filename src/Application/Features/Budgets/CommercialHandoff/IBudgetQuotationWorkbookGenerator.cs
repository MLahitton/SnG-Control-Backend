using SnG.Control.Domain.Enums;
using SnG.Control.Domain.ValueObjects;

namespace SnG.Control.Application.Features.Budgets.CommercialHandoff;

public sealed record ConfirmedWorkbookItem(CommercialItemSection Section, int SourceRow, CatalogItemValues Extracted, CatalogItemValues Confirmed);

public interface IBudgetQuotationWorkbookGenerator
{
    // Null means that the stored workbook cannot safely represent the persisted technical items.
    Task<byte[]?> GenerateAsync(ReadOnlyMemory<byte> original, string documentSgCode,
        IReadOnlyList<ConfirmedWorkbookItem> items, CancellationToken cancellationToken);
}
