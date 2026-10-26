using SnG.Control.Domain.Entities;

namespace SnG.Control.Application.Features.Projects;

public interface IProjectStore
{
    Task<Project?> GetReadableByIdAsync(Guid id, SnG.Control.Application.Access.ProjectReadScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> ListReadableAsync(SnG.Control.Application.Access.ProjectReadScope scope, CancellationToken cancellationToken);
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken);
}
