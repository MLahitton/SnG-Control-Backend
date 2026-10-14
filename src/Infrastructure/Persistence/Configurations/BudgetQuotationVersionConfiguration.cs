using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Configurations;

public sealed class BudgetQuotationVersionConfiguration : IEntityTypeConfiguration<BudgetQuotationVersion>
{
    public const string VersionIndexName = "UX_BudgetQuotationVersions_ProjectId_VersionNumber";
    public void Configure(EntityTypeBuilder<BudgetQuotationVersion> builder)
    {
        builder.ToTable("BudgetQuotationVersions", table => table.HasCheckConstraint("CK_BudgetQuotationVersions_VersionNumber", "\"VersionNumber\" > 0"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.ConfirmedAt).HasColumnType("timestamp with time zone");
        builder.HasOne<Project>().WithMany().HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.ProjectId, value.VersionNumber }).IsUnique().HasDatabaseName(VersionIndexName);
        builder.HasIndex(value => value.OriginalFileStorageKey).IsUnique();
        builder.HasIndex(value => value.ConfirmedFileStorageKey).IsUnique();
    }
}
