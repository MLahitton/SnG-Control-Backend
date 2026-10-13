using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Tests.TestDoubles;

public class ProjectTaskStoreDouble : IProjectTaskStore
{
    public List<ProjectTask> Items { get; } = [];
    public List<CancellationToken> ReceivedTokens { get; } = [];
    public int AddCalls { get; private set; }

    public Task AddAsync(ProjectTask task, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        AddCalls++;
        Items.Add(task);
        return Task.CompletedTask;
    }

    public Task<bool> HasPrepareQuotationAsync(Guid projectId, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Items.Any(task =>
            task.ProjectId == projectId && task.Type == ProjectTaskType.PrepareQuotation));
    }

    public Task<IReadOnlyList<ProjectTask>> ListByAreaAsync(
        OperationalArea area, ProjectTaskStatus status, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectTask>>(
            Items.Where(task => task.Area == area && task.Status == status).ToArray());
    }

    public Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectTask>>(Items.Where(task => task.ProjectId == projectId).ToArray());
    }
}
