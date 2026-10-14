using SnG.Control.Domain.Enums;

namespace SnG.Control.Domain.Entities;

public class ProjectTask
{
    public ProjectTask(Guid projectId, OperationalArea area, ProjectTaskType type, string? assignedToGoogleSubject = null)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        }
        if (!Enum.IsDefined(area))
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "Area must be a defined value.");
        }
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Type must be a defined value.");
        }

        if (assignedToGoogleSubject is not null) ArgumentException.ThrowIfNullOrWhiteSpace(assignedToGoogleSubject);
        AssignedToGoogleSubject = assignedToGoogleSubject;
        Id = Guid.NewGuid();
        ProjectId = projectId;
        Area = area;
        Type = type;
        Status = ProjectTaskStatus.Pending;
        AssignedAt = DateTime.UtcNow;
        StartedAt = null;
        CompletedAt = null;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string? AssignedToGoogleSubject { get; private set; }
    public OperationalArea Area { get; private set; }
    public ProjectTaskType Type { get; private set; }
    public ProjectTaskStatus Status { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public void Start()
    {
        if (Status != ProjectTaskStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending task can be started.");
        }

        Status = ProjectTaskStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        CompletedAt = null;
    }

    public void Complete()
    {
        if (Status != ProjectTaskStatus.InProgress || StartedAt is null || CompletedAt is not null)
            throw new InvalidOperationException("Only an active, started task can be completed.");
        Status = ProjectTaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }
}
