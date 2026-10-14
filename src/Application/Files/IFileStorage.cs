namespace SnG.Control.Application.Files;

public interface IFileStorage
{
    Task<byte[]> ReadAsync(string storageKey, CancellationToken cancellationToken);
    Task SaveAsync(string storageKey, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
