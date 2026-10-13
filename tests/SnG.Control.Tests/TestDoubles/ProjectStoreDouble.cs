using SnG.Control.Application.Features.Projects;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Tests.TestDoubles;

public class ProjectStoreDouble : IProjectStore
{
    public List<Project> Items { get; } = [];
    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task AddAsync(Project entity, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Items.SingleOrDefault(entity => entity.Id == id));
    }

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Project>>(Items.ToArray());
    }
}
