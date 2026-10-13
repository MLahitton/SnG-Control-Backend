namespace SnG.Control.Application.Features.Clients.List;

public class ListClientsUseCase(IClientStore store)
{
    public async Task<IReadOnlyList<ClientResponse>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var entities = await store.ListAsync(cancellationToken);
        return entities.Select(ClientResponse.FromEntity).ToArray();
    }
}
