using FluentValidation;
using System.Text.Json;
using AwesomeAssertions;
using EmpireIdle.Application.Common.Behaviors;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Common;

public class IdempotencyBehaviorTests
{
    private const string ValidKey = "abcdefghijklmnop-0123";   // 21 символ, у межах 16–128

    private readonly IIdempotencyRepository _repository = Substitute.For<IIdempotencyRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();
    private readonly IRequestContext _requestContext = Substitute.For<IRequestContext>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly Guid _playerId = Guid.NewGuid();

    private sealed record PayCommand(int Gems) : IRequest<string>, IIdempotentRequest;

    private sealed record PlainCommand(int Gems) : IRequest<string>;

    public IdempotencyBehaviorTests()
    {
        _currentPlayer.PlayerId.Returns(_playerId);
        _requestContext.IdempotencyKey.Returns(ValidKey);
        _repository.FindAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IdempotencyRecord?)null);
        _repository.TryReserveAsync(Arg.Any<IdempotencyRecord>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private IdempotencyBehavior<TRequest, string> BehaviorFor<TRequest>() where TRequest : notnull
        => new(_repository, _currentPlayer, _requestContext, _time,
               NullLogger<IdempotencyBehavior<TRequest, string>>.Instance);

    private static RequestHandlerDelegate<string> Handler(string result = "receipt-1", Action? onCall = null)
        => _ =>
        {
            onCall?.Invoke();
            return Task.FromResult(result);
        };

    private IdempotencyRecord StoredRecord(string requestType, string? responseJson) =>
        new(Guid.NewGuid(), ValidKey, _playerId, requestType, responseJson,
            _time.GetUtcNow().UtcDateTime);

    // ---------- Happy path ----------

    [Fact]
    public async Task Handle_ShouldPassThrough_WhenRequestIsNotIdempotent()
    {
        var behavior = BehaviorFor<PlainCommand>();

        var result = await behavior.Handle(new PlainCommand(10), Handler(), CancellationToken.None);

        result.Should().Be("receipt-1");
        await _repository.DidNotReceiveWithAnyArgs().TryReserveAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReserveKey_BeforeRunningHandler()
    {
        // Резерв мусить статися ДО виконання: інакше два паралельні запити
        // обидва пройдуть перевірку FindAsync і обидва спишуть гроші
        var reservedBeforeHandler = false;

        _repository.TryReserveAsync(Arg.Any<IdempotencyRecord>(), Arg.Any<CancellationToken>())
            .Returns(_ => { reservedBeforeHandler = true; return true; });

        var behavior = BehaviorFor<PayCommand>();
        var handlerSawReservation = false;

        await behavior.Handle(
            new PayCommand(10),
            Handler(onCall: () => handlerSawReservation = reservedBeforeHandler),
            CancellationToken.None);

        handlerSawReservation.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPersistSerialisedResponse_AfterSuccess()
    {
        // РЕГРЕСІЯ B3: у поточній версії record додається в окремий (вже закритий)
        // контекст, тому SetResponse + UnitOfWork.SaveChanges нічого не пишуть.
        var behavior = BehaviorFor<PayCommand>();

        await behavior.Handle(new PayCommand(10), Handler("receipt-42"), CancellationToken.None);

        await _repository.Received(1).CompleteAsync(
            Arg.Any<Guid>(),
            Arg.Is<string>(json => json == JsonSerializer.Serialize("receipt-42")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStampRecordWithProvidedClock()
    {
        var behavior = BehaviorFor<PayCommand>();

        await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        await _repository.Received(1).TryReserveAsync(
            Arg.Is<IdempotencyRecord>(r => r.CreatedAt == _time.GetUtcNow().UtcDateTime),
            Arg.Any<CancellationToken>());
    }

    // ---------- Replay ----------

    [Fact]
    public async Task Handle_ShouldReplayStoredResponse_WithoutRunningHandler()
    {
        var stored = StoredRecord(nameof(PayCommand), JsonSerializer.Serialize("receipt-42"));

        _repository.FindAsync(_playerId, ValidKey, Arg.Any<CancellationToken>()).Returns(stored);

        var behavior = BehaviorFor<PayCommand>();
        var handlerCalled = false;

        var result = await behavior.Handle(
            new PayCommand(10),
            Handler(onCall: () => handlerCalled = true),
            CancellationToken.None);

        result.Should().Be("receipt-42");
        handlerCalled.Should().BeFalse("повтор не має списувати гроші вдруге");
    }

    [Fact]
    public async Task Handle_ShouldReplayLosersResponse_WhenReservationRaceIsLost()
    {
        // Унікальний індекс відсік другий паралельний запит — він має отримати
        // результат переможця, а не помилку
        var winner = StoredRecord(nameof(PayCommand), JsonSerializer.Serialize("receipt-winner"));

        _repository.TryReserveAsync(Arg.Any<IdempotencyRecord>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.FindAsync(_playerId, ValidKey, Arg.Any<CancellationToken>())
            .Returns((IdempotencyRecord?)null, winner);

        var behavior = BehaviorFor<PayCommand>();

        var result = await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        result.Should().Be("receipt-winner");
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenKeyWasUsedForDifferentOperation()
    {
        var stored = StoredRecord("SomeOtherCommand", JsonSerializer.Serialize("x"));

        _repository.FindAsync(_playerId, ValidKey, Arg.Any<CancellationToken>()).Returns(stored);

        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already used for a different operation*");
    }

    [Fact]
    public async Task Handle_ShouldTellClientToRetry_WhenOperationIsStillInFlight()
    {
        // Резерв є, відповіді ще нема — операція виконується просто зараз
        var inFlight = StoredRecord(nameof(PayCommand), responseJson: null);

        _repository.FindAsync(_playerId, ValidKey, Arg.Any<CancellationToken>()).Returns(inFlight);

        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*still in progress*");
    }

    // ---------- Failure handling ----------

    [Fact]
    public async Task Handle_ShouldReleaseReservation_WhenHandlerThrows()
    {
        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(
            new PayCommand(10),
            _ => throw new InvalidOperationException("not enough gems"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _repository.Received(1).ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotStoreResponse_WhenHandlerThrows()
    {
        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(
            new PayCommand(10),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _repository.DidNotReceiveWithAnyArgs().CompleteAsync(default, default, default);
    }

    [Fact]
    public async Task Handle_ShouldReleaseReservation_EvenWhenRequestWasCancelled()
    {
        // Скасування не має лишати ключ зайнятим: гравець закрив вкладку
        // й повторив дію — ключ той самий
        var behavior = BehaviorFor<PayCommand>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await behavior.Handle(
            new PayCommand(10),
            _ => throw new OperationCanceledException(cts.Token),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _repository.Received(1).ReleaseAsync(Arg.Any<Guid>(), CancellationToken.None);
    }

    // ---------- Key validation ----------
    [Fact]
    public async Task Handle_ShouldReject_WhenHeaderIsMissing()
    {
        _requestContext.IdempotencyKey.Returns((string?)null);

        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        // PropertyName має збігатися з іменем заголовка: GlobalExceptionHandler
        // групує Errors саме по ньому, і це те, що клієнт побачить у відповіді
        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("Idempotency-Key");
    }

    [Theory]
    [InlineData("short")]                                    // < 16 символів
    [InlineData("key with spaces and enough length")]        // пробіли не в наборі
    [InlineData("ключ-достатньої-довжини-кирилицею")]        // не ASCII
    [InlineData("bad/slash/key/with/enough/length")]         // слеш не в наборі
    public async Task Handle_ShouldReject_WhenKeyIsMalformed(string key)
    {
        _requestContext.IdempotencyKey.Returns(key);

        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*16–128 chars*");
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenKeyExceedsMaximumLength()
    {
        _requestContext.IdempotencyKey.Returns(new string('a', 129));

        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ShouldAccept_KeyAtExactBoundaryLengths()
    {
        foreach (var key in new[] { new string('a', 16), new string('b', 128) })
        {
            _requestContext.IdempotencyKey.Returns(key);
            _repository.ClearReceivedCalls();

            var behavior = BehaviorFor<PayCommand>();

            var result = await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

            result.Should().Be("receipt-1");
        }
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenThereIsNoAuthenticatedPlayer()
    {
        _currentPlayer.PlayerId.Returns((Guid?)null);

        var behavior = BehaviorFor<PayCommand>();

        var act = async () => await behavior.Handle(new PayCommand(10), Handler(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
