using SnG.Control.Domain.Enums;
using SnG.Control.Domain.ValueObjects;

namespace SnG.Control.Domain.Entities;

public sealed class CommercialCatalogItemExtraction
{
    private CommercialCatalogItemExtraction() { }
    public CommercialCatalogItemExtraction(Guid catalogItemId, CatalogItemValues values,
        CatalogItemImageStatus imageStatus, string? imageStorageKey, string? imageContentType)
    {
        if (catalogItemId == Guid.Empty) throw new ArgumentException("Catalog item cannot be empty.");
        ArgumentNullException.ThrowIfNull(values);
        if (!Enum.IsDefined(imageStatus)) throw new ArgumentOutOfRangeException(nameof(imageStatus));
        if (imageStatus == CatalogItemImageStatus.Stored)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imageStorageKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(imageContentType);
            if (imageStorageKey.StartsWith('/') || imageStorageKey.Contains('\\') || imageStorageKey.Contains(':')
                || imageStorageKey.Split('/').Any(segment => segment is "" or "." or ".."))
                throw new ArgumentException("Image storage key must be relative.");
        }
        else if (imageStorageKey is not null || imageContentType is not null) throw new ArgumentException("Unavailable image cannot have a file.");
        CatalogItemId = catalogItemId; ImageStatus = imageStatus; ImageStorageKey = imageStorageKey; ImageContentType = imageContentType;
        ItemNumber = values.ItemNumber; Location = values.Location; System = values.System; Glass = values.Glass; AluminumFinish = values.AluminumFinish; LockType = values.LockType; Observations = values.Observations; Width = values.Width; Height = values.Height; SquareMeters = values.SquareMeters; Quantity = values.Quantity;
    }
    public Guid CatalogItemId { get; private set; }
    public CatalogItemImageStatus ImageStatus { get; private set; }
    public string? ImageStorageKey { get; private set; }
    public string? ImageContentType { get; private set; }
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
