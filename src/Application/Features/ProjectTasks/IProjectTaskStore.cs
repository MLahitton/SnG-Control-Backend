using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.ProjectTasks;

public interface IProjectTaskStore
{
    Task AddAsync(ProjectTask task, CancellationToken cancellationToken);
    Task<bool> HasPrepareQuotationAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectTask>> ListByAreaAsync(
        OperationalArea area, ProjectTaskStatus status, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken);
}
