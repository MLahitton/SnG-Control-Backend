using Microsoft.EntityFrameworkCore;
using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Infrastructure.Persistence.Stores;

public class EfProjectTaskStore(SnGControlDbContext context) : IProjectTaskStore
{
    public async Task AddAsync(ProjectTask task, CancellationToken cancellationToken)
    {
        await context.ProjectTasks.AddAsync(task, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasPrepareQuotationAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.ProjectTasks.AsNoTracking().AnyAsync(
            task => task.ProjectId == projectId && task.Type == ProjectTaskType.PrepareQuotation, cancellationToken);

    public async Task<IReadOnlyList<ProjectTask>> ListByAreaAsync(
        OperationalArea area, ProjectTaskStatus status, CancellationToken cancellationToken) =>
        await context.ProjectTasks.AsNoTracking()
            .Where(task => task.Area == area && task.Status == status)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.ProjectTasks.AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .ToListAsync(cancellationToken);
}
