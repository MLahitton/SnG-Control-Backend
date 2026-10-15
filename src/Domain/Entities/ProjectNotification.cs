using System.Security.Cryptography;
using System.Text;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Domain.Entities;

public sealed class ProjectNotification
{
    private ProjectNotification() { }
    public ProjectNotification(ProjectTask task, NotificationKind kind, string title, string message, DateTime scheduledAt, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task.AssignedToGoogleSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(title); ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (scheduledAt.Kind != DateTimeKind.Utc || createdAt.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC timestamps required.");
        Id = Guid.NewGuid(); ProjectId = task.ProjectId; ProjectTaskId = task.Id;
        RecipientGoogleSubject = task.AssignedToGoogleSubject; Kind = kind; Title = title; Message = message;
        ScheduledAt = scheduledAt; CreatedAt = createdAt;
        State = kind == NotificationKind.Overdue ? NotificationState.Active : NotificationState.Informational;
        EventKey = Key(task, kind);
    }
    public static string Key(ProjectTask task, NotificationKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task.AssignedToGoogleSubject);
        var recipient = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(task.AssignedToGoogleSubject)));
        var cycle = task.AssignedAt.Ticks - task.AssignedAt.Ticks % 10;
        return FormattableString.Invariant($"{task.Id:D}:{(int)kind}:{recipient}:{cycle}");
    }
    public void Resolve(DateTime completedAt)
    {
        if (Kind != NotificationKind.Overdue || completedAt.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Only overdue notifications can be resolved at a valid UTC completion instant.");
        if (State == NotificationState.Resolved) return;
        State = NotificationState.Resolved; ResolvedAt = completedAt;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ProjectTaskId { get; private set; }
    public string RecipientGoogleSubject { get; private set; } = "";
    public NotificationKind Kind { get; private set; }
    public NotificationState State { get; private set; }
    public string Title { get; private set; } = "";
    public string Message { get; private set; } = "";
    public DateTime ScheduledAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string EventKey { get; private set; } = "";
}
