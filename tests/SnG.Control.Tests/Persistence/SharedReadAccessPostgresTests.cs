using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SnG.Control.Application.Access;
using SnG.Control.Application.Features.Notifications;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Infrastructure.Persistence;
using SnG.Control.Infrastructure.Persistence.Stores;
using SnG.Control.Tests.TestDoubles;

namespace SnG.Control.Tests.Persistence;

public class SharedReadAccessPostgresTests
{
    [QuotationPostgresFact]
    public async Task ScopedSqlQueriesMatchHistoricalAccessAndDoNotTrackOrWrite()
    {
        var config = new ConfigurationBuilder().AddUserSecrets(typeof(SnG.Control.Api.DependencyInjection).Assembly).AddEnvironmentVariables().Build();
        var connection = config.GetConnectionString("SnGControl");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Local PostgreSQL configuration is required.");
        await using var db = new SnGControlDbContext(new DbContextOptionsBuilder<SnGControlDbContext>().UseNpgsql(connection).Options);
        var subject = "synthetic-" + Guid.NewGuid();
        var clients = Enumerable.Range(0, 4).Select(_ => new Client("Synthetic scope rollback")).ToArray();
        var projects = clients.Select((c, i) => new Project(c.Id, "Synthetic", "City", ProductLine.Classic, i == 0 ? subject : "synthetic-other")).ToArray();
        var budget = new ProjectTask(projects[1].Id, OperationalArea.Budgets, ProjectTaskType.PrepareQuotation, subject);
        var review = new ProjectTask(projects[2].Id, OperationalArea.Commercial, ProjectTaskType.ReviewQuotation, subject);
        var foreign = new ProjectTask(projects[3].Id, OperationalArea.Budgets, ProjectTaskType.PrepareQuotation, "synthetic-other");
        foreach (var task in new[] { budget, review, foreign }) { task.Start(); task.Complete(); }
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.AddRange(clients); db.AddRange(projects); db.AddRange(budget, review, foreign); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var projectStore = new EfProjectStore(db); var clientStore = new EfClientStore(db);
            var taskStore = new EfProjectTaskStore(db, TestTiming.Value, new EfProjectNotificationWriter(db, new ProjectNotificationPlanner(TestTiming.Value)));
            foreach (var role in new[] { SystemRole.CommercialAdvisor, SystemRole.Budgets, SystemRole.CommercialHead, SystemRole.Management })
            {
                var scope = new ProjectReadScope(subject, role);
                var allowedIndex = role == SystemRole.CommercialAdvisor ? 0 : role == SystemRole.Budgets ? 1 : 2;
                var listed = await projectStore.ListReadableAsync(scope, default);
                var listedClients = await clientStore.ListReadableAsync(scope, default);
                var tasks = await taskStore.ListByAreaAsync(OperationalArea.Budgets, ProjectTaskStatus.Completed, scope, default);
                for (var i = 0; i < projects.Length; i++)
                {
                    var allowed = role == SystemRole.Management || i == allowedIndex;
                    Assert.Equal(allowed, listed.Any(p => p.Id == projects[i].Id));
                    Assert.Equal(allowed, await projectStore.GetReadableByIdAsync(projects[i].Id, scope, default) is not null);
                    var clientAllowed = allowed || role == SystemRole.CommercialAdvisor;
                    Assert.Equal(clientAllowed, listedClients.Any(c => c.Id == clients[i].Id));
                    Assert.Equal(clientAllowed, await clientStore.GetReadableByIdAsync(clients[i].Id, scope, default) is not null);
                    Assert.Equal(allowed && i is 1 or 3, tasks.Any(t => t.ProjectId == projects[i].Id));
                }
            }
            Assert.Empty(await projectStore.ListReadableAsync(new(subject, null), default));
            Assert.Empty(db.ChangeTracker.Entries());
        }
        finally { await transaction.RollbackAsync(); }
        var ids = projects.Select(p => p.Id).ToArray();
        Assert.False(await db.Projects.AnyAsync(p => ids.Contains(p.Id)));
    }
}
