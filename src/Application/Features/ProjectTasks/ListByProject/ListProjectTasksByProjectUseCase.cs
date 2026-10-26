using SnG.Control.Application.Access;
using SnG.Control.Application.Scheduling;
using SnG.Control.Application.Features.Projects;

namespace SnG.Control.Application.Features.ProjectTasks.ListByProject;

public class ListProjectTasksByProjectUseCase(IProjectStore projects, IProjectTaskStore tasks, TaskTiming timing, ProjectReadAccess access)
{
    public async Task<IReadOnlyList<ProjectTaskResponse>?> ExecuteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await projects.GetReadableByIdAsync(projectId, access.Scope, cancellationToken) is null)
        {
            return null;
        }

        var entities = await tasks.ListByProjectAsync(projectId, cancellationToken);
        return entities.Select(task => ProjectTaskResponse.FromEntity(task, timing)).ToArray();
    }
}
