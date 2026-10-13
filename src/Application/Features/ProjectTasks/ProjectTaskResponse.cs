using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Application.Features.ProjectTasks;

public sealed record ProjectTaskResponse(
    Guid Id, Guid ProjectId, OperationalArea Area, ProjectTaskType Type, ProjectTaskStatus Status,
    DateTime AssignedAt, DateTime? StartedAt, DateTime? CompletedAt)
{
    internal static ProjectTaskResponse FromEntity(ProjectTask task) => new(
        task.Id, task.ProjectId, task.Area, task.Type, task.Status,
        task.AssignedAt, task.StartedAt, task.CompletedAt);
}
