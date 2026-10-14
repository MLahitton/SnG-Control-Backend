using Microsoft.EntityFrameworkCore;
using SnG.Control.Domain.Entities;
using SnG.Control.Infrastructure.Persistence;
using SnG.Control.Infrastructure.Persistence.Configurations;

namespace SnG.Control.Tests.Persistence;

public class QuotationCatalogModelTests
{
    [Fact]
    public void CatalogModel_HasRelationalSnapshotsRestrictionsAndNoBinaryOrPriceColumns()
    {
        using var db = new SnGControlDbContext(new DbContextOptionsBuilder<SnGControlDbContext>().UseNpgsql().Options);
        var version = db.Model.FindEntityType(typeof(BudgetQuotationVersion))!;
        var catalog = db.Model.FindEntityType(typeof(CommercialCatalog))!;
        var item = db.Model.FindEntityType(typeof(CommercialCatalogItem))!;
        var extraction = db.Model.FindEntityType(typeof(CommercialCatalogItemExtraction))!;
        Assert.Equal("BudgetQuotationVersions", version.GetTableName()); Assert.Equal("CommercialCatalogs", catalog.GetTableName());
        Assert.Equal("CommercialCatalogItems", item.GetTableName()); Assert.Equal("CommercialCatalogItemExtractions", extraction.GetTableName());
        var unique = Assert.Single(version.GetIndexes(), index => index.GetDatabaseName() == BudgetQuotationVersionConfiguration.VersionIndexName);
        Assert.True(unique.IsUnique); Assert.Equal(new[] { "ProjectId", "VersionNumber" }, unique.Properties.Select(property => property.Name));
        Assert.Contains(version.GetIndexes(), index => index.IsUnique && index.Properties[0].Name == "OriginalFileStorageKey");
        Assert.True(Assert.Single(catalog.GetForeignKeys()).IsUnique);
        Assert.Contains(item.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "CommercialCatalogId", "SourceRow" }));
        Assert.Contains(item.GetIndexes(), index => !index.IsUnique && index.Properties[0].Name == "CommercialCatalogId");
        Assert.Equal("CatalogItemId", Assert.Single(extraction.FindPrimaryKey()!.Properties).Name);
        Assert.True(Assert.Single(extraction.GetForeignKeys()).IsUnique);
        Assert.Equal("integer", item.FindProperty("Section")!.GetColumnType()); Assert.Equal("integer", extraction.FindProperty("ImageStatus")!.GetColumnType());
        foreach (var entity in new[] { version, catalog, item, extraction })
        {
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
            Assert.DoesNotContain(entity.GetIndexes(), index => index.IsUnique && index.Properties.Any(property => property.Name == "ItemNumber"));
            foreach (var property in entity.GetProperties())
            {
                Assert.DoesNotContain(property.GetColumnType(), new[] { "bytea", "json", "jsonb" });
                Assert.DoesNotContain("price", property.Name, StringComparison.OrdinalIgnoreCase);
                if (property.Name.EndsWith("StorageKey")) Assert.Equal("text", property.GetColumnType());
                if (property.ClrType == typeof(decimal)) Assert.Equal("numeric", property.GetColumnType());
            }
        }
        Assert.Contains("FROM \"CommercialCatalogItemExtractions\"", db.CommercialCatalogItemExtractions.ToQueryString());
    }
}
