using SnG.Control.Application.Scheduling;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.Management;

public sealed class ListManagementTasksUseCase(IManagementReadStore store, TaskTiming timing)
{
    public async Task<IReadOnlyList<ManagementTaskResponse>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var data = await store.ListTasksAsync(cancellationToken);
        var now = timing.UtcNow;
        return data.Select(row =>
        {
            var task = row.Task;
            return new ManagementTaskResponse(task.Id, task.ProjectId, row.ProjectName, row.ClientId,
                row.ClientName, row.SgCode, row.City, row.ProductLine, task.Area, task.Type, task.Status,
                task.AssignedAt, task.StartedAt, task.CompletedAt, task.DeadlineAt, task.WasOverdue, timing.Status(task, now));
        }).OrderBy(row => row.Status == ProjectTaskStatus.Completed)
            .ThenBy(row => Priority(row.LeakTimeStatus))
            .ThenBy(row => row.Status == ProjectTaskStatus.Completed ? (DateTime?)null : row.DeadlineAt ?? DateTime.MaxValue)
            .ThenByDescending(row => row.Status == ProjectTaskStatus.Completed ? row.CompletedAt : null)
            .ThenBy(row => row.ProjectName, StringComparer.Ordinal)
            .ThenBy(row => row.TaskId).ToArray();
    }

    private static int Priority(LeakTimeStatus status) => status switch
    {
        LeakTimeStatus.Overdue => 0,
        LeakTimeStatus.DueToday => 1,
        LeakTimeStatus.DueSoon => 2,
        LeakTimeStatus.OnTrack => 3,
        LeakTimeStatus.NotStarted => 4,
        LeakTimeStatus.CompletedLate => 0,
        LeakTimeStatus.CompletedOnTime => 1,
        _ => 5 // UntrackedLegacy, including completed tasks without historical timing.
    };
}
