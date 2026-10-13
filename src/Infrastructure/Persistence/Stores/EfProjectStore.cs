using Microsoft.EntityFrameworkCore;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Stores;

public class EfProjectStore(SnGControlDbContext context) : IProjectStore
{
    public async Task AddAsync(Project entity, CancellationToken cancellationToken)
    {
        await context.Projects.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Projects.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) =>
        await context.Projects.AsNoTracking().ToListAsync(cancellationToken);
}
