using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Infrastructure.Persistence;

namespace SnG.Control.Tests.Persistence;

public class SnGControlDbContextTests
{
    [Theory]
    [InlineData(typeof(Client), "Clients", 4)]
    [InlineData(typeof(Project), "Projects", 8)]
    [InlineData(typeof(ProjectTask), "ProjectTasks", 8)]
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
    public void Queries_CompileForAllEntitiesWithoutConnectionOrParameterlessConstructors()
    {
        using var context = CreateContext();

        Assert.Contains("FROM \"Clients\"", context.Clients.ToQueryString());
        Assert.Contains("FROM \"Projects\"", context.Projects.ToQueryString());
        Assert.Contains("FROM \"ProjectTasks\"", context.ProjectTasks.ToQueryString());
        Assert.True(string.IsNullOrEmpty(context.Database.GetConnectionString()));
    }

    [Fact]
    public void Migrations_ContainExactlyExpectedMigrationsAndMatchCurrentModel()
    {
        using var context = CreateContext();

        Assert.Equal(new[] { "InitialCreate", "AddProjectTasks", "EnforceUniquePrepareQuotationTask", "EnforceSgCodeFormat", "AddBudgetQuotationCatalog" },
            context.Database.GetMigrations().Select(migration => migration[(migration.IndexOf('_') + 1)..]));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Model_MapsTaskNullabilityAndUtcTimestamps()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ProjectTask))!;

        foreach (var property in entity.GetProperties())
        {
            Assert.Equal(property.Name is nameof(ProjectTask.StartedAt) or nameof(ProjectTask.CompletedAt), property.IsNullable);
            Assert.False(property.IsShadowProperty());
            Assert.True(property.PropertyInfo!.SetMethod!.IsPrivate);
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
            {
                Assert.Equal("timestamp with time zone", property.GetColumnType());
            }
        }
    }

    [Fact]
    public void Model_RelatesTasksToProjectsRestrictivelyAndPreservesQueryIndexesWithPartialUniqueness()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ProjectTask))!;
        var foreignKey = Assert.Single(entity.GetForeignKeys());

        Assert.Equal(nameof(ProjectTask.ProjectId), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal("uuid", foreignKey.Properties[0].GetColumnType());
        Assert.Equal(typeof(Project), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Project.Id), Assert.Single(foreignKey.PrincipalKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Null(foreignKey.DependentToPrincipal);
        Assert.Null(foreignKey.PrincipalToDependent);
        var indexes = entity.GetIndexes().ToArray();
        Assert.Equal(3, indexes.Length);
        var byProject = Assert.Single(indexes, index => index.GetDatabaseName() == "IX_ProjectTasks_ProjectId");
        Assert.False(byProject.IsUnique);
        Assert.Null(byProject.GetFilter());
        Assert.Equal(nameof(ProjectTask.ProjectId), Assert.Single(byProject.Properties).Name);
        var byArea = Assert.Single(indexes, index => index.GetDatabaseName() == "IX_ProjectTasks_Area_Status");
        Assert.False(byArea.IsUnique);
        Assert.Null(byArea.GetFilter());
        Assert.Equal(new[] { nameof(ProjectTask.Area), nameof(ProjectTask.Status) }, byArea.Properties.Select(property => property.Name));
        var unique = Assert.Single(indexes, index => index.IsUnique);
        Assert.Equal("UX_ProjectTasks_ProjectId_PrepareQuotation", unique.GetDatabaseName());
        Assert.Equal(nameof(ProjectTask.ProjectId), Assert.Single(unique.Properties).Name);
        Assert.Equal("\"Type\" = 1", unique.GetFilter());
    }

    [Theory]
    [InlineData(nameof(ProjectTask.Area), OperationalArea.Commercial, 1)]
    [InlineData(nameof(ProjectTask.Area), OperationalArea.Budgets, 2)]
    [InlineData(nameof(ProjectTask.Type), ProjectTaskType.PrepareQuotation, 1)]
    [InlineData(nameof(ProjectTask.Status), ProjectTaskStatus.Pending, 1)]
    [InlineData(nameof(ProjectTask.Status), ProjectTaskStatus.InProgress, 2)]
    [InlineData(nameof(ProjectTask.Status), ProjectTaskStatus.Completed, 3)]
    public void Model_StoresTaskEnumsAsStableIntegers(string propertyName, object value, int storedValue)
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(ProjectTask))!.FindProperty(propertyName)!;
        var converter = property.GetTypeMapping().Converter!;

        Assert.Equal("integer", property.GetColumnType());
        Assert.Equal(storedValue, converter.ConvertToProvider(value));
        Assert.Equal(value, converter.ConvertFromProvider(storedValue));
    }

    private static SnGControlDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SnGControlDbContext>()
            .UseNpgsql()
            .Options;

        return new SnGControlDbContext(options);
    }

    [Fact]
    public void Model_EnforcesSgFormatWhilePreservingNullableUniqueSgCode()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Project))!;
        var check = Assert.Single(entity.GetCheckConstraints());
        Assert.Equal("CK_Projects_SgCode_Format", check.Name);
        Assert.Equal("\"SgCode\" IS NULL OR \"SgCode\" ~ '^SG[0-9]+$'", check.Sql);
        var index = Assert.Single(entity.GetIndexes(), index => index.GetDatabaseName() == "IX_Projects_SgCode");
        Assert.True(index.IsUnique);
        Assert.Null(index.GetFilter());
        Assert.True(entity.FindProperty(nameof(Project.SgCode))!.IsNullable);
        Assert.Equal("text", entity.FindProperty(nameof(Project.SgCode))!.GetColumnType());
    }
}
