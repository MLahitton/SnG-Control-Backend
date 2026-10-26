using SnG.Control.Application.Access;
namespace SnG.Control.Application.Features.Projects.List;

public class ListProjectsUseCase(IProjectStore store, ProjectReadAccess access)
{
    public async Task<IReadOnlyList<ProjectResponse>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var entities = await store.ListReadableAsync(access.Scope, cancellationToken);
        return entities.Select(ProjectResponse.FromEntity).ToArray();
    }
}
