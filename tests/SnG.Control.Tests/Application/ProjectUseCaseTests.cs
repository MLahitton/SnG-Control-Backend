using SnG.Control.Application.Features.Projects.Create;
using SnG.Control.Application.Features.Projects.GetById;
using SnG.Control.Application.Features.Projects.List;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Tests.TestDoubles;

namespace SnG.Control.Tests.Application;

public class ProjectUseCaseTests
{
    [Fact]
    public async Task CreateGetAndList_UseExistingClientKeepSgNullAndAllowRepeatedNames()
    {
        var clients = new ClientStoreDouble();
        var projects = new ProjectStoreDouble();
        var client = new Client("Cliente");
        clients.Items.Add(client);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var create = new CreateProjectUseCase(projects, clients);

        var first = await create.ExecuteAsync(client.Id, " Casa Campestre ", " Bucaramanga ", ProductLine.Signature, token);
        var second = await create.ExecuteAsync(client.Id, "Casa Campestre", "Bucaramanga", ProductLine.Signature, token);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(client.Id, first.ClientId);
        Assert.Equal("Casa Campestre", first.Name);
        Assert.Equal("Bucaramanga", first.City);
        Assert.Equal(ProductLine.Signature, first.ProductLine);
        Assert.Null(first.SgCode);
        Assert.Null(second.SgCode);
        Assert.Equal(projects.Items[0].CreatedAt, first.CreatedAt);
        Assert.Equal(projects.Items[0].UpdatedAt, first.UpdatedAt);
        Assert.Equal(first, await new GetProjectByIdUseCase(projects).ExecuteAsync(first.Id, token));
        Assert.Null(await new GetProjectByIdUseCase(projects).ExecuteAsync(Guid.NewGuid(), token));
        var listed = await new ListProjectsUseCase(projects).ExecuteAsync(token);
        Assert.Equal(2, listed.Count);
        Assert.Contains(first, listed);
        Assert.Contains(second, listed);
        Assert.All(clients.ReceivedTokens.Concat(projects.ReceivedTokens), received => Assert.Equal(token, received));
    }

    [Fact]
    public async Task Create_WhenClientDoesNotExist_ReturnsNullWithoutWriting()
    {
        var clients = new ClientStoreDouble();
        var projects = new ProjectStoreDouble();
        var result = await new CreateProjectUseCase(projects, clients).ExecuteAsync(
            Guid.NewGuid(), "Casa", "Ciudad", ProductLine.Classic, CancellationToken.None);
        Assert.Null(result);
        Assert.Empty(projects.Items);
        Assert.Empty(projects.ReceivedTokens);
        Assert.Single(clients.ReceivedTokens);
    }

    [Fact]
    public async Task List_WhenEmpty_ReturnsEmptyCollection()
    {
        Assert.Empty(await new ListProjectsUseCase(new ProjectStoreDouble()).ExecuteAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("clientId")]
    [InlineData("name")]
    [InlineData("city")]
    [InlineData("productLine")]
    public async Task Create_WithInvalidInput_DoesNotReadOrWrite(string invalidField)
    {
        var clients = new ClientStoreDouble();
        var projects = new ProjectStoreDouble();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            new CreateProjectUseCase(projects, clients).ExecuteAsync(
                invalidField == "clientId" ? Guid.Empty : Guid.NewGuid(),
                invalidField == "name" ? " " : "Casa",
                invalidField == "city" ? " " : "Ciudad",
                invalidField == "productLine" ? (ProductLine)0 : ProductLine.Classic,
                CancellationToken.None));
        Assert.Empty(clients.ReceivedTokens);
        Assert.Empty(projects.ReceivedTokens);
    }

    [Fact]
    public async Task Create_PropagatesCancellationDuringClientLookupWithoutWriting()
    {
        var clients = new ClientStoreDouble();
        var projects = new ProjectStoreDouble();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CreateProjectUseCase(projects, clients).ExecuteAsync(
                Guid.NewGuid(), "Casa", "Ciudad", ProductLine.Classic, cancellation.Token));
        Assert.Empty(projects.Items);
        Assert.Empty(projects.ReceivedTokens);
    }
}
