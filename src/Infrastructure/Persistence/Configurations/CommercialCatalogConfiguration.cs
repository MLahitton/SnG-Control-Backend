using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Configurations;

public sealed class CommercialCatalogConfiguration : IEntityTypeConfiguration<CommercialCatalog>
{
    public void Configure(EntityTypeBuilder<CommercialCatalog> builder)
    {
        builder.ToTable("CommercialCatalogs");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasOne<BudgetQuotationVersion>().WithOne().HasForeignKey<CommercialCatalog>(value => value.BudgetQuotationVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
