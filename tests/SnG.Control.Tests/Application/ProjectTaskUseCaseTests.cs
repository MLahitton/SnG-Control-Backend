using SnG.Control.Application.Features.ProjectTasks.SendToBudgets;
using SnG.Control.Application.Features.ProjectTasks.ListByArea;
using SnG.Control.Application.Features.ProjectTasks.ListByProject;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Tests.TestDoubles;

namespace SnG.Control.Tests.Application;

public class ProjectTaskUseCaseTests
{
    [Fact]
    public async Task Send_CreatesPendingBudgetsTask_AndMapsRejectedInsertToAlreadySent()
    {
        var projects = new ProjectStoreDouble();
        var tasks = new ProjectTaskStoreDouble();
        var project = new Project(Guid.NewGuid(), "Proyecto", "Ciudad", ProductLine.Classic);
        projects.Items.Add(project);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var send = new SendProjectToBudgetsUseCase(projects, tasks);

        var result = Assert.IsType<SendProjectToBudgetsResult.Created>(await send.ExecuteAsync(project.Id, token));
        var saved = Assert.Single(tasks.Items);
        Assert.Equal(saved.Id, result.Task.Id);
        Assert.Equal(project.Id, result.Task.ProjectId);
        Assert.Equal(OperationalArea.Budgets, result.Task.Area);
        Assert.Equal(ProjectTaskType.PrepareQuotation, result.Task.Type);
        Assert.Equal(ProjectTaskStatus.Pending, result.Task.Status);
        Assert.Equal(saved.AssignedAt, result.Task.AssignedAt);
        Assert.Null(result.Task.StartedAt);
        Assert.Null(result.Task.CompletedAt);
        Assert.IsType<SendProjectToBudgetsResult.AlreadySent>(await send.ExecuteAsync(project.Id, token));
        Assert.Equal(2, tasks.InsertAttempts);
        Assert.Equal(2, tasks.ReceivedTokens.Count);
        Assert.Single(tasks.Items);
        Assert.All(projects.ReceivedTokens.Concat(tasks.ReceivedTokens), received => Assert.Equal(token, received));
    }

    [Fact]
    public async Task Send_MissingProject_DoesNotReadTasksOrWrite()
    {
        var tasks = new ProjectTaskStoreDouble();
        var result = await new SendProjectToBudgetsUseCase(new ProjectStoreDouble(), tasks)
            .ExecuteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<SendProjectToBudgetsResult.ProjectNotFound>(result);
        Assert.Empty(tasks.ReceivedTokens);
        Assert.Equal(0, tasks.InsertAttempts);
    }

    [Theory]
    [InlineData(ProjectTaskStatus.Pending)]
    [InlineData(ProjectTaskStatus.InProgress)]
    [InlineData(ProjectTaskStatus.Completed)]
    public async Task Send_StoreRejectsExistingQuotationInAnyStatus_ReturnsAlreadySent(ProjectTaskStatus status)
    {
        var projects = new ProjectStoreDouble();
        var tasks = new ProjectTaskStoreDouble();
        var project = new Project(Guid.NewGuid(), "Proyecto", "Ciudad", ProductLine.Classic);
        projects.Items.Add(project);
        tasks.Items.Add(StoredTask(project.Id, OperationalArea.Commercial, status));

        var result = await new SendProjectToBudgetsUseCase(projects, tasks).ExecuteAsync(project.Id, CancellationToken.None);

        Assert.IsType<SendProjectToBudgetsResult.AlreadySent>(result);
        Assert.Equal(1, tasks.InsertAttempts);
        Assert.Single(tasks.Items);
    }

    [Fact]
    public async Task Lists_FilterByAreaStatusAndProject_AndPropagateCancellation()
    {
        var projects = new ProjectStoreDouble();
        var tasks = new ProjectTaskStoreDouble();
        var project = new Project(Guid.NewGuid(), "Proyecto", "Ciudad", ProductLine.Classic);
        projects.Items.Add(project);
        var pending = StoredTask(project.Id, OperationalArea.Budgets, ProjectTaskStatus.Pending);
        var completed = StoredTask(project.Id, OperationalArea.Budgets, ProjectTaskStatus.Completed);
        var commercial = StoredTask(Guid.NewGuid(), OperationalArea.Commercial, ProjectTaskStatus.Pending);
        tasks.Items.AddRange([pending, completed, commercial]);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        var byArea = await new ListProjectTasksByAreaUseCase(tasks).ExecuteAsync(
            OperationalArea.Budgets, ProjectTaskStatus.Pending, token);
        Assert.Equal(pending.Id, Assert.Single(byArea).Id);

        var history = await new ListProjectTasksByProjectUseCase(projects, tasks).ExecuteAsync(project.Id, token);
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, task => task.Id == pending.Id);
        Assert.Contains(history, task => task.Id == completed.Id && task.Status == ProjectTaskStatus.Completed);
        Assert.All(projects.ReceivedTokens.Concat(tasks.ReceivedTokens), received => Assert.Equal(token, received));
    }

    [Fact]
    public async Task Lists_ReturnEmptyForExistingProjectAndNoAreaMatches_ButNullForMissingProject()
    {
        var projects = new ProjectStoreDouble();
        var tasks = new ProjectTaskStoreDouble();
        var project = new Project(Guid.NewGuid(), "Proyecto", "Ciudad", ProductLine.Classic);
        projects.Items.Add(project);
        var history = new ListProjectTasksByProjectUseCase(projects, tasks);

        Assert.Empty((await history.ExecuteAsync(project.Id, CancellationToken.None))!);
        var reads = tasks.ReceivedTokens.Count;
        Assert.Null(await history.ExecuteAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(reads, tasks.ReceivedTokens.Count);
        Assert.Empty(await new ListProjectTasksByAreaUseCase(tasks).ExecuteAsync(
            OperationalArea.Budgets, ProjectTaskStatus.Pending, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 4)]
    public async Task ListByArea_InvalidEnums_FailBeforeReading(int area, int status)
    {
        var tasks = new ProjectTaskStoreDouble();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListProjectTasksByAreaUseCase(tasks).ExecuteAsync(
                (OperationalArea)area, (ProjectTaskStatus)status, CancellationToken.None));
        Assert.Empty(tasks.ReceivedTokens);
    }

    [Fact]
    public async Task Send_CanceledToken_StopsBeforeTaskOperations()
    {
        var tasks = new ProjectTaskStoreDouble();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SendProjectToBudgetsUseCase(new ProjectStoreDouble(), tasks)
                .ExecuteAsync(Guid.NewGuid(), cancellation.Token));
        Assert.Empty(tasks.ReceivedTokens);
    }

    private static ProjectTask StoredTask(Guid projectId, OperationalArea area, ProjectTaskStatus status)
    {
        var task = new ProjectTask(projectId, area, ProjectTaskType.PrepareQuotation);
        if (status == ProjectTaskStatus.InProgress) task.Start();
        else if (status == ProjectTaskStatus.Completed)
            typeof(ProjectTask).GetProperty(nameof(ProjectTask.Status))!.SetValue(task, status);
        return task;
    }
}
