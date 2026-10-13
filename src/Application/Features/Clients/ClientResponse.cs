using SnG.Control.Domain.Entities;

namespace SnG.Control.Application.Features.Clients;

public sealed record ClientResponse(Guid Id, string Name, DateTime CreatedAt, DateTime UpdatedAt)
{
    internal static ClientResponse FromEntity(Client entity) => new(
        entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);
}
