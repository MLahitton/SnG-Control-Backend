namespace SnG.Control.Application.Features.Projects.List;

public class ListProjectsUseCase(IProjectStore store)
{
    public async Task<IReadOnlyList<ProjectResponse>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var entities = await store.ListAsync(cancellationToken);
        return entities.Select(ProjectResponse.FromEntity).ToArray();
    }
}
