using Microsoft.EntityFrameworkCore;
using SnG.Control.Domain.Entities;
using SnG.Control.Infrastructure.Persistence.Configurations;

namespace SnG.Control.Infrastructure.Persistence;

public class SnGControlDbContext(DbContextOptions<SnGControlDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
    }
}
