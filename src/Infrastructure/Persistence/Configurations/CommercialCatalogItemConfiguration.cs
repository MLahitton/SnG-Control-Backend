using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Configurations;

public sealed class CommercialCatalogItemConfiguration : IEntityTypeConfiguration<CommercialCatalogItem>
{
    public void Configure(EntityTypeBuilder<CommercialCatalogItem> builder)
    {
        builder.ToTable("CommercialCatalogItems", table =>
        {
            table.HasCheckConstraint("CK_CommercialCatalogItems_SourceRow", "\"SourceRow\" > 0");
            table.HasCheckConstraint("CK_CommercialCatalogItems_Section", "\"Section\" IN (1, 2)");
            table.HasCheckConstraint("CK_CommercialCatalogItems_Values", "\"Width\" > 0 AND \"Height\" > 0 AND \"SquareMeters\" > 0 AND \"Quantity\" > 0 AND length(btrim(\"ItemNumber\")) > 0");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Section).HasConversion<int>();
        builder.HasOne<CommercialCatalog>().WithMany().HasForeignKey(value => value.CommercialCatalogId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => value.CommercialCatalogId);
        builder.HasIndex(value => new { value.CommercialCatalogId, value.SourceRow }).IsUnique();
    }
}
