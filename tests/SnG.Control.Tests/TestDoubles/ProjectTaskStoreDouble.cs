using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Tests.TestDoubles;

public class ProjectTaskStoreDouble : IProjectTaskStore
{
    public List<ProjectTask> Items { get; } = [];
    public List<CancellationToken> ReceivedTokens { get; } = [];
    public int InsertAttempts { get; private set; }

    public Task<bool> TryAddPrepareQuotationAsync(ProjectTask task, CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        InsertAttempts++;
        if (Items.Any(existing => existing.ProjectId == task.ProjectId && existing.Type == ProjectTaskType.PrepareQuotation))
        {
            return Task.FromResult(false);
        }
        Items.Add(task);
        return Task.FromResult(true);
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
