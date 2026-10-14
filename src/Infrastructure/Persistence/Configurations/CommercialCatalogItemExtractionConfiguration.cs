using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Configurations;

public sealed class CommercialCatalogItemExtractionConfiguration : IEntityTypeConfiguration<CommercialCatalogItemExtraction>
{
    public void Configure(EntityTypeBuilder<CommercialCatalogItemExtraction> builder)
    {
        builder.ToTable("CommercialCatalogItemExtractions", table =>
        {
            table.HasCheckConstraint("CK_CommercialCatalogItemExtractions_Values", "\"Width\" > 0 AND \"Height\" > 0 AND \"SquareMeters\" > 0 AND \"Quantity\" > 0 AND length(btrim(\"ItemNumber\")) > 0");
            table.HasCheckConstraint("CK_CommercialCatalogItemExtractions_Image", "(\"ImageStatus\" = 1 AND \"ImageStorageKey\" IS NOT NULL AND \"ImageContentType\" IS NOT NULL) OR (\"ImageStatus\" IN (2, 3) AND \"ImageStorageKey\" IS NULL AND \"ImageContentType\" IS NULL)");
        });
        builder.HasKey(value => value.CatalogItemId);
        builder.Property(value => value.CatalogItemId).ValueGeneratedNever();
        builder.Property(value => value.ImageStatus).HasConversion<int>();
        builder.HasOne<CommercialCatalogItem>().WithOne().HasForeignKey<CommercialCatalogItemExtraction>(value => value.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => value.ImageStorageKey).IsUnique();
    }
}
