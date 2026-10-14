namespace SnG.Control.Tests.Fixtures;

public sealed class TemporaryStorage : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "sng-quotation-tests-" + Guid.NewGuid().ToString("N"));
    public TemporaryStorage() => Directory.CreateDirectory(Root);
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
}
