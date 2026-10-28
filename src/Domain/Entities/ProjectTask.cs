using SnG.Control.Domain.Enums;

namespace SnG.Control.Domain.Entities;

public class ProjectTask
{
    private ProjectTask() { }
    public ProjectTask(Guid projectId, OperationalArea area, ProjectTaskType type, string? assignedToGoogleSubject = null, DateTime? assignedAtUtc = null, int quotationRound = 1)
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
        if (quotationRound <= 0) throw new ArgumentOutOfRangeException(nameof(quotationRound));
        QuotationRound = quotationRound;
        Id = Guid.NewGuid();
        ProjectId = projectId;
        Area = area;
        Type = type;
        Status = ProjectTaskStatus.Pending;
        AssignedAt = assignedAtUtc ?? DateTime.UtcNow;
        RequireUtc(AssignedAt);
        StartedAt = null;
        CompletedAt = null;
    }

    public int? QuotationRound { get; private set; }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string? AssignedToGoogleSubject { get; private set; }
    public OperationalArea Area { get; private set; }
    public ProjectTaskType Type { get; private set; }
    public ProjectTaskStatus Status { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? DeadlineAt { get; private set; }
    public bool? WasOverdue { get; private set; }

    public void Start(DateTime startedAtUtc, DateTime deadlineAtUtc)
    {
        RequireUtc(startedAtUtc); RequireUtc(deadlineAtUtc);
        if (deadlineAtUtc <= startedAtUtc) throw new ArgumentException("Deadline must follow task start.", nameof(deadlineAtUtc));
        if (Status != ProjectTaskStatus.Pending || StartedAt is not null || CompletedAt is not null || DeadlineAt is not null || WasOverdue is not null)
        {
            throw new InvalidOperationException("Only a pending task can be started.");
        }

        Status = ProjectTaskStatus.InProgress;
        StartedAt = startedAtUtc;
        DeadlineAt = deadlineAtUtc;
        WasOverdue = false;
        CompletedAt = null;
    }

    public void Complete(DateTime completedAtUtc)
    {
        RequireUtc(completedAtUtc);
        if (Status != ProjectTaskStatus.InProgress || StartedAt is null || CompletedAt is not null)
            throw new InvalidOperationException("Only an active, started task can be completed.");
        if (completedAtUtc < StartedAt) throw new ArgumentException("Completion cannot precede start.", nameof(completedAtUtc));
        Status = ProjectTaskStatus.Completed;
        CompletedAt = completedAtUtc;
        if (DeadlineAt is DateTime deadline) WasOverdue = WasOverdue == true || completedAtUtc > deadline;
    }

    public void DetectOverdue(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (Status == ProjectTaskStatus.InProgress && DeadlineAt is DateTime deadline && nowUtc > deadline) WasOverdue = true;
    }

    private static void RequireUtc(DateTime value)
    { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("An explicit UTC instant is required."); }
}
