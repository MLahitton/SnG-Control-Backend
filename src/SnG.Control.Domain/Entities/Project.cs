using SnG.Control.Domain.Enums;

namespace SnG.Control.Domain.Entities;

public class Project
{
    public Project(Guid clientId, string name, string city, ProductLine productLine)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("ClientId cannot be empty.", nameof(clientId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        if (!Enum.IsDefined(productLine))
        {
            throw new ArgumentOutOfRangeException(nameof(productLine), productLine, "ProductLine must be a defined value.");
        }

        Id = Guid.NewGuid();
        ClientId = clientId;
        Name = name.Trim();
        City = city.Trim();
        ProductLine = productLine;
        SgCode = null;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; }
    public Guid ClientId { get; }
    public string Name { get; }
    public string City { get; }
    public ProductLine ProductLine { get; }
    public string? SgCode { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }
}
