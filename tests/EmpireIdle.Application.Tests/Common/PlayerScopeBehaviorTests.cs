using AwesomeAssertions;
using EmpireIdle.Application.Common.Behaviors;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Common;

public class PlayerScopeBehaviorTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Guid _playerId = Guid.NewGuid();

    private sealed record ScopedQuery(Guid PlayerId) : IRequest<string>, IPlayerScopedRequest;

    private sealed record PlainQuery : IRequest<string>;

    public PlayerScopeBehaviorTests() => _currentPlayer.PlayerId.Returns(_playerId);

    private PlayerScopeBehavior<TRequest, string> BehaviorFor<TRequest>() where TRequest : notnull
        => new(_currentPlayer, _players, _time,
               NullLogger<PlayerScopeBehavior<TRequest, string>>.Instance);

    private static RequestHandlerDelegate<string> Handler(string result = "ok", Action? onCall = null)
        => _ =>
        {
            onCall?.Invoke();

            return Task.FromResult(result);
        };

    [Fact]
    public async Task Own_request_passes_and_records_presence()
    {
        var result = await BehaviorFor<ScopedQuery>()
            .Handle(new ScopedQuery(_playerId), Handler(), default);

        result.Should().Be("ok");

        await _players.Received(1).TouchLastSeenAsync(
            _playerId, Now.UtcDateTime, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Foreign_player_id_is_rejected_and_leaves_no_trace()
    {
        var handlerCalled = false;

        var act = () => BehaviorFor<ScopedQuery>()
            .Handle(new ScopedQuery(Guid.NewGuid()), Handler(onCall: () => handlerCalled = true), default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        handlerCalled.Should().BeFalse();

        // Відхилений запит присутністю не рахується
        await _players.DidNotReceiveWithAnyArgs()
            .TouchLastSeenAsync(default, default, default, default);
    }

    [Fact]
    public async Task Request_without_an_authenticated_player_is_rejected()
    {
        _currentPlayer.PlayerId.Returns((Guid?)null);

        var act = () => BehaviorFor<ScopedQuery>()
            .Handle(new ScopedQuery(_playerId), Handler(), default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        await _players.DidNotReceiveWithAnyArgs()
            .TouchLastSeenAsync(default, default, default, default);
    }

    [Fact]
    public async Task Unscoped_request_is_left_alone()
    {
        // Фонові джоби ходять саме такими запитами: ні перевірки, ні присутності
        _currentPlayer.PlayerId.Returns((Guid?)null);

        var result = await BehaviorFor<PlainQuery>().Handle(new PlainQuery(), Handler(), default);

        result.Should().Be("ok");

        await _players.DidNotReceiveWithAnyArgs()
            .TouchLastSeenAsync(default, default, default, default);
    }

    [Fact]
    public async Task Failed_presence_write_does_not_break_the_response()
    {
        _players.TouchLastSeenAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("db is down"));

        // Присутність — телеметрія: її збій не має ставати помилкою гравця
        var result = await BehaviorFor<ScopedQuery>()
            .Handle(new ScopedQuery(_playerId), Handler(), default);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handler_failure_is_not_counted_as_presence()
    {
        RequestHandlerDelegate<string> failing = _ => throw new InvalidOperationException("boom");

        var act = () => BehaviorFor<ScopedQuery>().Handle(new ScopedQuery(_playerId), failing, default);

        await act.Should().ThrowAsync<InvalidOperationException>();

        await _players.DidNotReceiveWithAnyArgs()
            .TouchLastSeenAsync(default, default, default, default);
    }
}
