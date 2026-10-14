using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.Projects;

public sealed record ProjectResponse(Guid Id, Guid ClientId, string Name, string City,
    ProductLine ProductLine, string? SgCode, DateTime CreatedAt, DateTime UpdatedAt, ProjectCreatorResponse? CreatedBy)
{
    internal static ProjectResponse FromEntity(Project entity) => new(
        entity.Id, entity.ClientId, entity.Name, entity.City,
        entity.ProductLine, entity.SgCode, entity.CreatedAt, entity.UpdatedAt,
        entity.CreatedByGoogleSubject is null ? null : new(entity.CreatedByGoogleSubject, entity.CreatedByEmail, entity.CreatedByName));
}

public sealed record ProjectCreatorResponse(string Subject, string? Email, string? Name);
