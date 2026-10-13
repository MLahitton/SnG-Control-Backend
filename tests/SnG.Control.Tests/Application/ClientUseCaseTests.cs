using SnG.Control.Application.Features.Clients.Create;
using SnG.Control.Application.Features.Clients.GetById;
using SnG.Control.Application.Features.Clients.List;
using SnG.Control.Tests.TestDoubles;

namespace SnG.Control.Tests.Application;

public class ClientUseCaseTests
{
    [Fact]
    public async Task CreateGetAndList_MapPersistedClientsAndAllowRepeatedNames()
    {
        var store = new ClientStoreDouble();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var create = new CreateClientUseCase(store);

        var first = await create.ExecuteAsync("  Cliente Demo  ", token);
        var second = await create.ExecuteAsync("Cliente Demo", token);

        Assert.Equal("Cliente Demo", first.Name);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(store.Items[0].CreatedAt, first.CreatedAt);
        Assert.Equal(store.Items[0].UpdatedAt, first.UpdatedAt);
        Assert.Equal(first, await new GetClientByIdUseCase(store).ExecuteAsync(first.Id, token));
        Assert.Null(await new GetClientByIdUseCase(store).ExecuteAsync(Guid.NewGuid(), token));
        var listed = await new ListClientsUseCase(store).ExecuteAsync(token);
        Assert.Equal(2, listed.Count);
        Assert.Contains(first, listed);
        Assert.Contains(second, listed);
        Assert.All(store.ReceivedTokens, received => Assert.Equal(token, received));
    }

    [Fact]
    public async Task List_WhenEmpty_ReturnsEmptyCollection()
    {
        Assert.Empty(await new ListClientsUseCase(new ClientStoreDouble()).ExecuteAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task Create_WithInvalidName_DoesNotWrite(string? name)
    {
        var store = new ClientStoreDouble();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            new CreateClientUseCase(store).ExecuteAsync(name!, CancellationToken.None));
        Assert.Empty(store.Items);
        Assert.Empty(store.ReceivedTokens);
    }

    [Fact]
    public async Task Create_PropagatesCancellationAndDoesNotWrite()
    {
        var store = new ClientStoreDouble();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CreateClientUseCase(store).ExecuteAsync("Cliente", cancellation.Token));
        Assert.Empty(store.Items);
    }
}
