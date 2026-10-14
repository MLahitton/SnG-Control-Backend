namespace SnG.Control.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string RootPath { get; set; } = "";
}
