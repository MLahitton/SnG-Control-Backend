using Microsoft.EntityFrameworkCore;
using SnG.Control.Domain.Entities;
using SnG.Control.Infrastructure.Persistence.Configurations;

namespace SnG.Control.Infrastructure.Persistence;

public class SnGControlDbContext(DbContextOptions<SnGControlDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<BudgetQuotationVersion> BudgetQuotationVersions => Set<BudgetQuotationVersion>();
    public DbSet<CommercialCatalog> CommercialCatalogs => Set<CommercialCatalog>();
    public DbSet<CommercialCatalogItem> CommercialCatalogItems => Set<CommercialCatalogItem>();
    public DbSet<CommercialCatalogItemExtraction> CommercialCatalogItemExtractions => Set<CommercialCatalogItemExtraction>();
    public DbSet<CommercialQuotation> CommercialQuotations => Set<CommercialQuotation>();
    public DbSet<CommercialQuotationItem> CommercialQuotationItems => Set<CommercialQuotationItem>();

    public DbSet<ProjectNotification> ProjectNotifications => Set<ProjectNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectTaskConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectNotificationConfiguration());
        modelBuilder.ApplyConfiguration(new BudgetQuotationVersionConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialCatalogConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialCatalogItemConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialCatalogItemExtractionConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialQuotationConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialQuotationItemConfiguration());
    }
}
