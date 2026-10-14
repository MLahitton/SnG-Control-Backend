using Microsoft.Extensions.Options;
using SnG.Control.Infrastructure.Files;
using SnG.Control.Tests.Fixtures;

namespace SnG.Control.Tests.Storage;

public class LocalFileStorageTests
{
    private static LocalFileStorage Storage(TemporaryStorage directory) => new(Options.Create(new FileStorageOptions { RootPath = directory.Root }));

    [Theory]
    [InlineData("projects/a/b/original.xlsx")]
    [InlineData("projects/a/b/images/item.png")]
    public async Task Save_IsByteExactCreatesDirectoriesAndDeleteOnlyRemovesItsFile(string key)
    {
        using var directory = new TemporaryStorage(); var storage = Storage(directory);
        var bytes = new QuotationWorkbook().Item().Build();
        await storage.SaveAsync(key, bytes, default);
        await storage.SaveAsync("unrelated.bin", new byte[] { 7 }, default);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(directory.Root, key)));
        await storage.DeleteAsync(key, default); await storage.DeleteAsync(key, default);
        Assert.False(File.Exists(Path.Combine(directory.Root, key)));
        Assert.Equal(new byte[] { 7 }, await File.ReadAllBytesAsync(Path.Combine(directory.Root, "unrelated.bin")));
        Assert.Empty(Directory.GetFiles(directory.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ExistingFile_IsNeverOverwrittenAndTemporaryIsRemovedOnRenameFailure()
    {
        using var directory = new TemporaryStorage(); var storage = Storage(directory);
        await storage.SaveAsync("test.bin", new byte[] { 1 }, default);
        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync("test.bin", new byte[] { 2 }, default));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(directory.Root, "test.bin")));
        Assert.Single(Directory.GetFiles(directory.Root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("inside/../../escape")]
    [InlineData("/absolute")]
    [InlineData("C:\\absolute")]
    [InlineData("inside\\..\\escape")]
    [InlineData("inside//file")]
    [InlineData("inside/./file")]
    [InlineData("file:stream")]
    [InlineData("")]
    public async Task UnsafeKeys_AreRejectedForSaveAndDelete(string key)
    {
        using var directory = new TemporaryStorage(); var storage = Storage(directory);
        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(key, new byte[] { 1 }, default));
        await Assert.ThrowsAsync<IOException>(async () => await storage.DeleteAsync(key, default));
        Assert.Empty(Directory.GetFiles(directory.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cancellation_DoesNotLeavePartialFiles()
    {
        using var directory = new TemporaryStorage(); var storage = Storage(directory);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync("nested/file.bin", new byte[1000], cancellation.Token));
        Assert.Empty(Directory.GetFiles(directory.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Options_RequireAbsoluteDirectoryAndCanCreateIt()
    {
        using var directory = new TemporaryStorage(); var validator = new FileStorageOptionsValidator();
        Assert.False(validator.Validate(null, new()).Succeeded);
        Assert.False(validator.Validate(null, new() { RootPath = "relative" }).Succeeded);
        var path = Path.Combine(directory.Root, "created");
        Assert.True(validator.Validate(null, new() { RootPath = path }).Succeeded); Assert.True(Directory.Exists(path));
        var file = Path.Combine(directory.Root, "file"); File.WriteAllText(file, "test");
        Assert.False(validator.Validate(null, new() { RootPath = file }).Succeeded);
    }
}
