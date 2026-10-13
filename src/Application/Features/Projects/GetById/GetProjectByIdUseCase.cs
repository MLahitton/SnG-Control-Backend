namespace SnG.Control.Application.Features.Projects.GetById;

public class GetProjectByIdUseCase(IProjectStore store)
{
    public async Task<ProjectResponse?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await store.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : ProjectResponse.FromEntity(entity);
    }
}
