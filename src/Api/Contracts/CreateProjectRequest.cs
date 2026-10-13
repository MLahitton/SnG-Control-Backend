using SnG.Control.Domain.Enums;

namespace SnG.Control.Api.Contracts;

public sealed record CreateProjectRequest(Guid ClientId, string Name, string City, ProductLine ProductLine);
