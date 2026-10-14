using SnG.Control.Domain.Enums;
using SnG.Control.Domain.ValueObjects;

namespace SnG.Control.Domain.Entities;

public sealed class CommercialCatalogItem
{
    private CommercialCatalogItem() { }
    public CommercialCatalogItem(Guid commercialCatalogId, CommercialItemSection section, int sourceRow, CatalogItemValues values)
    {
        if (commercialCatalogId == Guid.Empty) throw new ArgumentException("Catalog cannot be empty.");
        if (!Enum.IsDefined(section)) throw new ArgumentOutOfRangeException(nameof(section));
        if (sourceRow <= 0) throw new ArgumentOutOfRangeException(nameof(sourceRow));
        ArgumentNullException.ThrowIfNull(values);
        Id = Guid.NewGuid(); CommercialCatalogId = commercialCatalogId; Section = section; SourceRow = sourceRow;
        ItemNumber = values.ItemNumber; Location = values.Location; System = values.System; Glass = values.Glass; AluminumFinish = values.AluminumFinish; LockType = values.LockType; Observations = values.Observations; Width = values.Width; Height = values.Height; SquareMeters = values.SquareMeters; Quantity = values.Quantity;
    }
    public Guid Id { get; private set; }
    public Guid CommercialCatalogId { get; private set; }
    public CommercialItemSection Section { get; private set; }
    public int SourceRow { get; private set; }
    public string ItemNumber { get; private set; } = "";
    public string Location { get; private set; } = "";
    public string System { get; private set; } = "";
    public string Glass { get; private set; } = "";
    public string AluminumFinish { get; private set; } = "";
    public string LockType { get; private set; } = "";
    public string Observations { get; private set; } = "";
    public decimal Width { get; private set; }
    public decimal Height { get; private set; }
    public decimal SquareMeters { get; private set; }
    public int Quantity { get; private set; }
}
