using SnG.Control.Domain.Entities;

namespace SnG.Control.Application.Features.Clients;

public interface IClientStore
{
    Task AddAsync(Client client, CancellationToken cancellationToken);
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
