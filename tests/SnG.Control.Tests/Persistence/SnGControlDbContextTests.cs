using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Infrastructure.Persistence;

namespace SnG.Control.Tests.Persistence;

public class SnGControlDbContextTests
{
    [Theory]
    [InlineData(typeof(Client), "Clients", 4)]
    [InlineData(typeof(Project), "Projects", 8)]
    public void Model_MapsEntitiesWithDomainGeneratedPrimaryKeys(Type entityType, string tableName, int propertyCount)
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(entityType)!;

        Assert.NotNull(entity);
        Assert.Equal(tableName, entity.GetTableName());
        Assert.Equal(propertyCount, entity.GetProperties().Count());
        var key = Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal("Id", key.Name);
        Assert.Equal("uuid", key.GetColumnType());
        Assert.Equal(ValueGenerated.Never, key.ValueGenerated);
    }

    [Theory]
    [InlineData(typeof(Client))]
    [InlineData(typeof(Project))]
    public void Model_RequiresAllPropertiesExceptSgCodeAndMapsUtcTimestamps(Type entityType)
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(entityType)!;

        foreach (var property in entity.GetProperties())
        {
            Assert.Equal(property.Name == nameof(Project.SgCode), property.IsNullable);
            Assert.False(property.IsShadowProperty());
            Assert.True(property.PropertyInfo!.SetMethod!.IsPrivate);

            if (property.ClrType == typeof(DateTime))
            {
                Assert.Equal("timestamp with time zone", property.GetColumnType());
            }

            if (property.ClrType == typeof(string))
            {
                Assert.Equal("text", property.GetColumnType());
                Assert.Null(property.GetMaxLength());
            }
        }
    }

    [Fact]
    public void Model_RelatesProjectsToClientsWithRequiredRestrictiveForeignKey()
    {
        using var context = CreateContext();
        var project = context.Model.FindEntityType(typeof(Project))!;
        var foreignKey = Assert.Single(project.GetForeignKeys());

        Assert.Equal(nameof(Project.ClientId), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(typeof(Client), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Client.Id), Assert.Single(foreignKey.PrincipalKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Null(foreignKey.DependentToPrincipal);
        Assert.Null(foreignKey.PrincipalToDependent);
    }

    [Fact]
    public void Model_HasStandardUniqueSgCodeIndexAndAllowsRepeatedNames()
    {
        using var context = CreateContext();
        var client = context.Model.FindEntityType(typeof(Client))!;
        var project = context.Model.FindEntityType(typeof(Project))!;
        var uniqueIndex = Assert.Single(project.GetIndexes(), index => index.IsUnique);

        Assert.Equal(nameof(Project.SgCode), Assert.Single(uniqueIndex.Properties).Name);
        Assert.Null(uniqueIndex.GetFilter());
        Assert.True(project.FindProperty(nameof(Project.SgCode))!.IsNullable);
        Assert.DoesNotContain(client.GetIndexes(), index => index.IsUnique);
        Assert.Contains(project.GetIndexes(), index => !index.IsUnique
            && index.Properties.Single().Name == nameof(Project.ClientId));
    }

    [Theory]
    [InlineData(ProductLine.Classic, 1)]
    [InlineData(ProductLine.Essential, 2)]
    [InlineData(ProductLine.Bioconfort, 3)]
    [InlineData(ProductLine.Signature, 4)]
    public void Model_StoresProductLineAsStableInteger(ProductLine productLine, int storedValue)
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(Project))!.FindProperty(nameof(Project.ProductLine))!;
        var converter = property.GetTypeMapping().Converter!;

        Assert.Equal("integer", property.GetColumnType());
        Assert.Equal(storedValue, converter.ConvertToProvider(productLine));
        Assert.Equal(productLine, converter.ConvertFromProvider(storedValue));
    }

    [Fact]
    public void Queries_CompileForBothEntitiesWithoutConnectionOrParameterlessConstructors()
    {
        using var context = CreateContext();

        Assert.Contains("FROM \"Clients\"", context.Clients.ToQueryString());
        Assert.Contains("FROM \"Projects\"", context.Projects.ToQueryString());
        Assert.True(string.IsNullOrEmpty(context.Database.GetConnectionString()));
    }

    [Fact]
    public void Migrations_ContainInitialCreateAndMatchCurrentModel()
    {
        using var context = CreateContext();

        Assert.EndsWith("_InitialCreate", Assert.Single(context.Database.GetMigrations()));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static SnGControlDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SnGControlDbContext>()
            .UseNpgsql()
            .Options;

        return new SnGControlDbContext(options);
    }
}
