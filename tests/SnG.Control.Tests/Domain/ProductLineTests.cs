using SnG.Control.Domain.Enums;

namespace SnG.Control.Tests.Domain;

public class ProductLineTests
{
    [Theory]
    [InlineData(ProductLine.Classic, 1)]
    [InlineData(ProductLine.Essential, 2)]
    [InlineData(ProductLine.Bioconfort, 3)]
    [InlineData(ProductLine.Signature, 4)]
    public void Values_MatchPersistedDataContract(ProductLine productLine, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)productLine);
    }
}
