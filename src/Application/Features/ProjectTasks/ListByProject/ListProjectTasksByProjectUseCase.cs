using SnG.Control.Application.Features.Projects;

namespace SnG.Control.Application.Features.ProjectTasks.ListByProject;

public class ListProjectTasksByProjectUseCase(IProjectStore projects, IProjectTaskStore tasks)
{
    public async Task<IReadOnlyList<ProjectTaskResponse>?> ExecuteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await projects.GetByIdAsync(projectId, cancellationToken) is null)
        {
            return null;
        }

        var entities = await tasks.ListByProjectAsync(projectId, cancellationToken);
        return entities.Select(ProjectTaskResponse.FromEntity).ToArray();
    }
}
