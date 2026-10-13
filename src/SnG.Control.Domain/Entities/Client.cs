namespace SnG.Control.Domain.Entities;

public class Client
{
    public Client(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = Guid.NewGuid();
        Name = name.Trim();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; }
    public string Name { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }
}
