using SnG.Control.Domain.Entities;

namespace SnG.Control.Application.Features.Projects;

public interface IProjectStore
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken);
}
