using Microsoft.EntityFrameworkCore;
using SnG.Control.Application.Features.Clients;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Stores;

public class EfClientStore(SnGControlDbContext context) : IClientStore
{
    public async Task AddAsync(Client entity, CancellationToken cancellationToken)
    {
        await context.Clients.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Clients.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken) =>
        await context.Clients.AsNoTracking().ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Clients.AsNoTracking().AnyAsync(client => client.Id == id, cancellationToken);
}
