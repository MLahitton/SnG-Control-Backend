namespace SnG.Control.Domain.ValueObjects;

public sealed record CatalogItemValues
{
    public CatalogItemValues(string itemNumber, string? location, string? system, string? glass,
        string? aluminumFinish, string? lockType, string? observations,
        decimal width, decimal height, decimal squareMeters, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemNumber);
        if (width <= 0 || height <= 0 || squareMeters <= 0 || quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Technical measures and quantity must be positive.");
        ItemNumber = itemNumber.Trim();
        Location = location?.Trim() ?? ""; System = system?.Trim() ?? ""; Glass = glass?.Trim() ?? "";
        AluminumFinish = aluminumFinish?.Trim() ?? ""; LockType = lockType?.Trim() ?? ""; Observations = observations?.Trim() ?? "";
        Width = width; Height = height; SquareMeters = squareMeters; Quantity = quantity;
    }
    public string ItemNumber { get; }
    public string Location { get; }
    public string System { get; }
    public string Glass { get; }
    public string AluminumFinish { get; }
    public string LockType { get; }
    public string Observations { get; }
    public decimal Width { get; }
    public decimal Height { get; }
    public decimal SquareMeters { get; }
    public int Quantity { get; }
}
