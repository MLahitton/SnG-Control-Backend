using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Tests.Domain;

public class ProjectTaskTests
{
    [Theory]
    [InlineData(OperationalArea.Commercial)]
    [InlineData(OperationalArea.Budgets)]
    public void Constructor_CreatesNewPendingAssignment(OperationalArea area)
    {
        var projectId = Guid.NewGuid();
        var before = DateTime.UtcNow;
        var task = new ProjectTask(projectId, area, ProjectTaskType.PrepareQuotation);
        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal(area, task.Area);
        Assert.Equal(ProjectTaskType.PrepareQuotation, task.Type);
        Assert.Equal(ProjectTaskStatus.Pending, task.Status);
        Assert.Equal(DateTimeKind.Utc, task.AssignedAt.Kind);
        Assert.InRange(task.AssignedAt, before, after);
        Assert.Null(task.StartedAt);
        Assert.Null(task.CompletedAt);
        Assert.NotEqual(task.Id, new ProjectTask(projectId, area, ProjectTaskType.PrepareQuotation).Id);
        Assert.All(typeof(ProjectTask).GetProperties(), property => Assert.True(property.SetMethod!.IsPrivate));
    }

    [Fact]
    public void Constructor_WithEmptyProjectId_Throws()
    {
        Assert.Throws<ArgumentException>("projectId",
            () => new ProjectTask(Guid.Empty, OperationalArea.Budgets, ProjectTaskType.PrepareQuotation));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    public void Constructor_WithUndefinedArea_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>("area",
            () => new ProjectTask(Guid.NewGuid(), (OperationalArea)value, ProjectTaskType.PrepareQuotation));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void Constructor_WithUndefinedType_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>("type",
            () => new ProjectTask(Guid.NewGuid(), OperationalArea.Budgets, (ProjectTaskType)value));
    }

    [Theory]
    [InlineData(OperationalArea.Commercial, 1)]
    [InlineData(OperationalArea.Budgets, 2)]
    public void AreaValues_MatchDataContract(OperationalArea area, int value) => Assert.Equal(value, (int)area);

    [Fact]
    public void TaskTypeValue_MatchesDataContract() => Assert.Equal(1, (int)ProjectTaskType.PrepareQuotation);

    [Theory]
    [InlineData(ProjectTaskStatus.Pending, 1)]
    [InlineData(ProjectTaskStatus.InProgress, 2)]
    [InlineData(ProjectTaskStatus.Completed, 3)]
    public void StatusValues_MatchDataContract(ProjectTaskStatus status, int value) => Assert.Equal(value, (int)status);
}
