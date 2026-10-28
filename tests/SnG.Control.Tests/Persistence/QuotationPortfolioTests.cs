using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SnG.Control.Application.Features.Management;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Domain.ValueObjects;
using SnG.Control.Infrastructure.Persistence;
using SnG.Control.Infrastructure.Persistence.Stores;

namespace SnG.Control.Tests.Persistence;

public sealed class PortfolioPostgresFactAttribute : FactAttribute
{
    public PortfolioPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNG_PORTFOLIO_TEST_DATABASE")))
            Skip = "Requires an isolated synthetic PostgreSQL database; never application configuration.";
    }
}

public sealed class QuotationPortfolioTests
{
    [Fact]
    public void AllFiveQueriesTranslateToPostgresWithServerAggregationOrderingAndLimit()
    {
        using var db = new SnGControlDbContext(new DbContextOptionsBuilder<SnGControlDbContext>().UseNpgsql().Options);
        var store = new EfManagementReadStore(db);
        Assert.Contains("sum(", store.QuotationSummaryQuery().ToQueryString());
        Assert.Contains("sum(", store.QuotationItemSummaryQuery().ToQueryString());
        foreach (var query in new[] { store.QuotationItemsQuery(false), store.QuotationItemsQuery(true) })
        {
            var sql = query.ToQueryString();
            Assert.Contains("LIMIT", sql); Assert.Contains("ORDER BY", sql); Assert.Contains("COLLATE", sql);
            Assert.Contains("INNER JOIN", sql); Assert.DoesNotContain("CommercialCatalogItemId", sql);
            Assert.Equal(4, query.Expression.ToString().Split("AsNoTracking()").Length - 1);
        }
        Assert.Contains("LIMIT", store.QuotationProjectsQuery().ToQueryString());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task UseCaseBuildsCoverageAndRejectsAggregateBeyondMoneyContract()
    {
        var store = new ManagementReadStoreDouble(new(), new(), new());
        var useCase = new GetManagementQuotationPortfolioUseCase(store);
        var empty = await useCase.ExecuteAsync(default);
        Assert.Equal(Money.CurrencyCode, empty.CurrencyCode); Assert.Equal(0.00m, empty.TotalQuotedValue);
        Assert.Empty(empty.HighestValueItems.Items); Assert.False(empty.HighestValueProjects.HasMore);
        store.Portfolio = store.Portfolio with { Summary = new(2, Money.Maximum + 1) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(default));
    }

    [PortfolioPostgresFact]
    public async Task RealQueriesAreBoundedExactStableAndDoNotExposeInternalIds()
    {
        var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SNG_PORTFOLIO_TEST_DATABASE"));
        Assert.Equal("127.0.0.1", config.Host); Assert.Equal("quotation_portfolio_test", config.Database);
        var counter = new QueryCounter();
        await using var db = new SnGControlDbContext(new DbContextOptionsBuilder<SnGControlDbContext>()
            .UseNpgsql(config.ConnectionString).AddInterceptors(counter).Options);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        await db.Database.MigrateAsync();
        var useCase = new GetManagementQuotationPortfolioUseCase(new EfManagementReadStore(db));
        counter.Reads = 0;
        var empty = await useCase.ExecuteAsync(default);
        Assert.Equal(5, counter.Reads);
        Assert.Equal(0, empty.QuotationCount); Assert.Equal(0, empty.ItemCount); Assert.Equal(0, empty.TotalQuantity);
        Assert.Equal(0.00m, empty.TotalQuotedValue); Assert.Empty(empty.HighestUnitPriceItems.Items);
        Assert.Empty(empty.HighestValueProjects.Items);

        var a = AddQuotation(db, "Proyecto A", 100.00m, [(4, 10m, 30m), (1, 25m, 25m)]);
        var b = AddQuotation(db, "Proyecto B", 250.00m, [(2, 20m, 200m), (1, 50m, 50m)]);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); counter.Reads = 0;
        var result = await useCase.ExecuteAsync(default);
        Assert.Equal(5, counter.Reads); Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(2, result.QuotationCount); Assert.Equal(4, result.ItemCount); Assert.Equal(8, result.TotalQuantity);
        Assert.Equal(350.00m, result.TotalQuotedValue); Assert.Equal("COP", result.CurrencyCode);
        Assert.Equal(b, result.HighestValueProjects.Items[0].ProjectId);
        Assert.Equal(200.00m, result.HighestValueItems.Items[0].TotalPrice);
        Assert.Equal("01", result.HighestValueItems.Items[0].ItemNumber);
        Assert.Equal("02", result.HighestUnitPriceItems.Items[0].ItemNumber);
        Assert.Equal(50.00m, result.HighestUnitPriceItems.Items[0].UnitPrice);
        Assert.Equal(30m, result.HighestValueItems.Items.Single(i => i.ProjectId == a && i.Quantity == 4).TotalPrice);
        Assert.Equal(4, result.HighestValueItems.Returned); Assert.False(result.HighestValueItems.HasMore);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var key in new[] { "commercialQuotationId", "commercialCatalogItemId", "storageKey", "subject", "\"id\"" })
            Assert.DoesNotContain(key, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("350.00", json);

        for (var i = 0; i < 12; i++) AddQuotation(db, "Tie", 250.00m, [(1, 50m, 200m), (1, 50m, 200m)]);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); counter.Reads = 0;
        result = await useCase.ExecuteAsync(default);
        Assert.Equal(5, counter.Reads);
        Assert.Equal(28, result.HighestValueItems.Total); Assert.Equal(14, result.HighestValueProjects.Total);
        Assert.Equal(10, result.HighestValueItems.Returned); Assert.True(result.HighestValueItems.HasMore);
        Assert.Equal(10, result.HighestUnitPriceItems.Returned); Assert.True(result.HighestUnitPriceItems.HasMore);
        Assert.Equal(10, result.HighestValueProjects.Returned); Assert.True(result.HighestValueProjects.HasMore);
        Assert.Equal("Proyecto B", result.HighestValueItems.Items[0].ProjectName);
        Assert.Equal("Proyecto B", result.HighestValueProjects.Items[0].ProjectName);
        Assert.All(result.HighestValueItems.Items.Skip(1), row => Assert.Equal("01", row.ItemNumber));
        var repeated = await useCase.ExecuteAsync(default);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(repeated));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static Guid AddQuotation(SnGControlDbContext db, string name, decimal total, (int Quantity, decimal Unit, decimal Total)[] rows)
    {
        var client = new Client("Synthetic portfolio client");
        var project = new Project(client.Id, name, "City", ProductLine.Classic, "synthetic-owner");
        var version = new BudgetQuotationVersion(project.Id, 1, "synthetic.xlsx", "Client", name, "City", "Line", "Line", "Author", "SG9001");
        var catalog = new CommercialCatalog(version.Id);
        var quotation = new CommercialQuotation(project.Id, version.Id, catalog.Id, "synthetic.xlsx", total, "synthetic-owner", null, null);
        db.AddRange(client, project, version, catalog, quotation);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var values = new CatalogItemValues($"{index + 1:00}", "Room", "System", "Glass", "Finish", "Lock", "Notes", 1m, 2m, 2m, row.Quantity);
            var item = new CommercialCatalogItem(catalog.Id, CommercialItemSection.Main, 15 + index, values);
            db.AddRange(item, new CommercialQuotationItem(quotation.Id, item.Id, item.Section, item.SourceRow, values, row.Unit, row.Total));
        }
        return project.Id;
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Reads { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++; return ValueTask.FromResult(result);
        }
    }
}
