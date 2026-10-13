namespace SnG.Control.Application.Features.Clients.GetById;

public class GetClientByIdUseCase(IClientStore store)
{
    public async Task<ClientResponse?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await store.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : ClientResponse.FromEntity(entity);
    }
}
