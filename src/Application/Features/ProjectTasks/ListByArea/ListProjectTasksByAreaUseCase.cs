using SnG.Control.Application.Scheduling;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.ProjectTasks.ListByArea;

public class ListProjectTasksByAreaUseCase(IProjectTaskStore tasks, TaskTiming timing)
{
    public async Task<IReadOnlyList<ProjectTaskResponse>> ExecuteAsync(
        OperationalArea area, ProjectTaskStatus status, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(area))
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "Area must be a defined value.");
        }
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Status must be a defined value.");
        }

        var entities = await tasks.ListByAreaAsync(area, status, cancellationToken);
        return entities.Select(task => ProjectTaskResponse.FromEntity(task, timing)).ToArray();
    }
}
