using SnG.Control.Application.Identity;
using SnG.Control.Application.Scheduling;

namespace SnG.Control.Application.Features.Notifications;

public sealed class ListPersonalNotificationsUseCase(IProjectNotificationStore store, ICurrentUser user)
{
    public Task<IReadOnlyList<ProjectNotificationResponse>> ExecuteAsync(CancellationToken token) => store.ListPersonalAsync(user.Subject, token);
}
public sealed class ListProjectNotificationsUseCase(IProjectNotificationStore store)
{
    public Task<IReadOnlyList<ProjectNotificationResponse>?> ExecuteAsync(Guid projectId, CancellationToken token) => store.ListProjectAsync(projectId, token);
}
public sealed class ProjectNotificationSweepService(IProjectNotificationSweepStore store, TaskTiming timing)
{
    public Task ExecuteAsync(CancellationToken token) => store.SweepAsync(timing.UtcNow, token);
}
