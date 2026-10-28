using Microsoft.EntityFrameworkCore;
using SnG.Control.Domain.Entities;
using SnG.Control.Infrastructure.Persistence.Configurations;

namespace SnG.Control.Infrastructure.Persistence;

public class SnGControlDbContext(DbContextOptions<SnGControlDbContext> options) : DbContext(options)
{
    public DbSet<AccountingHandoff> AccountingHandoffs => Set<AccountingHandoff>();
    public DbSet<InitialDeposit> InitialDeposits => Set<InitialDeposit>();
    public DbSet<RequotationRequest> RequotationRequests => Set<RequotationRequest>();
    public DbSet<ClientQuotationDecision> ClientQuotationDecisions => Set<ClientQuotationDecision>();
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
    public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();
    public DbSet<ProjectDocumentHandoff> ProjectDocumentHandoffs => Set<ProjectDocumentHandoff>();
    public DbSet<ProjectDocumentHandoffItem> ProjectDocumentHandoffItems => Set<ProjectDocumentHandoffItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AccountingHandoffConfiguration());
        modelBuilder.ApplyConfiguration(new InitialDepositConfiguration());
        modelBuilder.ApplyConfiguration(new RequotationRequestConfiguration());
        modelBuilder.ApplyConfiguration(new ClientQuotationDecisionConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectTaskConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectNotificationConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectDocumentHandoffConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectDocumentHandoffItemConfiguration());
        modelBuilder.ApplyConfiguration(new BudgetQuotationVersionConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialCatalogConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialCatalogItemConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialCatalogItemExtractionConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialQuotationConfiguration());
        modelBuilder.ApplyConfiguration(new CommercialQuotationItemConfiguration());
    }
}
