using SnG.Control.Application.Features.Clients;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Tests.TestDoubles;

public class ClientStoreDouble : IClientStore
{
    public List<Client> Items { get; } = [];
    public List<CancellationToken> ReceivedTokens { get; } = [];
    public bool FailReads { get; set; }

    public Task AddAsync(Client entity, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (FailReads) throw new InvalidOperationException("Internal SQL or connection detail that must never reach HTTP.");
        return Task.FromResult(Items.SingleOrDefault(entity => entity.Id == id));
    }

    public Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Client>>(Items.ToArray());
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Items.Any(entity => entity.Id == id));
    }
}
