using Microsoft.Extensions.Options;

namespace SnG.Control.Infrastructure.Files;

public sealed class FileStorageOptionsValidator : IValidateOptions<FileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, FileStorageOptions options)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.RootPath) || !Path.IsPathFullyQualified(options.RootPath))
                return ValidateOptionsResult.Fail("Configure FileStorage:RootPath as an absolute directory.");
            Directory.CreateDirectory(Path.GetFullPath(options.RootPath));
            return ValidateOptionsResult.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return ValidateOptionsResult.Fail("FileStorage:RootPath must be an accessible directory that exists or can be created."); }
    }
}
