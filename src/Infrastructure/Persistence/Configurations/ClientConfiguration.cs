using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(client => client.Id);
        builder.Property(client => client.Id).ValueGeneratedNever();
        builder.Property(client => client.Name).IsRequired();
        builder.Property(client => client.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(client => client.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
    }
}
