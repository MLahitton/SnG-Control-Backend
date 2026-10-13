using SnG.Control.Domain.Entities;

namespace SnG.Control.Application.Features.Clients.Create;

public class CreateClientUseCase(IClientStore store)
{
    public async Task<ClientResponse> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var client = new Client(name);
        await store.AddAsync(client, cancellationToken);
        return ClientResponse.FromEntity(client);
    }
}
