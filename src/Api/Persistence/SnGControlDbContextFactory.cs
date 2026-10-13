using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SnG.Control.Infrastructure.Persistence;

namespace SnG.Control.Api.Persistence;

public class SnGControlDbContextFactory : IDesignTimeDbContextFactory<SnGControlDbContext>
{
    public SnGControlDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(SnGControlDbContextFactory).Assembly.GetName().Name
        });
        var connectionString = builder.Configuration.GetConnectionString("SnGControl");
        var options = new DbContextOptionsBuilder<SnGControlDbContext>();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            options.UseNpgsql();
        }
        else
        {
            options.UseNpgsql(connectionString);
        }

        return new SnGControlDbContext(options.Options);
    }
}
