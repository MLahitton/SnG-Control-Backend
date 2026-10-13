using SnG.Control.Domain.Entities;

namespace SnG.Control.Tests.Domain;

public class ClientTests
{
    [Fact]
    public void Constructor_WithValidName_CreatesClientWithUtcTimestamps()
    {
        var before = DateTime.UtcNow;

        var client = new Client("Steel & Glass");

        var after = DateTime.UtcNow;
        Assert.NotEqual(Guid.Empty, client.Id);
        Assert.Equal("Steel & Glass", client.Name);
        Assert.Equal(DateTimeKind.Utc, client.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, client.UpdatedAt.Kind);
        Assert.InRange(client.CreatedAt, before, after);
        Assert.Equal(client.CreatedAt, client.UpdatedAt);
    }

    [Fact]
    public void Constructor_TrimsNameAndPreservesInternalWhitespace()
    {
        var client = new Client(" \tSteel  & Glass\r\n");

        Assert.Equal("Steel  & Glass", client.Name);
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("name", () => new Client(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithBlankName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>("name", () => new Client(name));
    }

    [Fact]
    public void Constructor_WithSameName_GeneratesDistinctIdentities()
    {
        var first = new Client("Steel & Glass");
        var second = new Client("Steel & Glass");

        Assert.NotEqual(first.Id, second.Id);
    }
}
