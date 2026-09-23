using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Tutorial.Commands;
using EmpireIdle.Application.Tutorial.Queries;
using EmpireIdle.Application.Tutorial.Validators;
using EmpireIdle.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Tutorial;

/// <summary>
/// Рядок прогресу з'являється лінивo на першому кроці, повтор кроку не пише в БД,
/// а гравець без рядка бачить порожній прогрес, а не 404.
/// </summary>
public class TutorialCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly ITutorialProgressRepository _repository = Substitute.For<ITutorialProgressRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private MarkTutorialStepSeenCommandHandler MarkSeen() => new(
        _repository, _unitOfWork, new FakeTimeProvider(Now), NullLogger<MarkTutorialStepSeenCommandHandler>.Instance);

    private SkipTutorialCommandHandler Skip() => new(
        _repository, _unitOfWork, new FakeTimeProvider(Now), NullLogger<SkipTutorialCommandHandler>.Instance);

    [Fact]
    public async Task MarkSeen_ShouldCreateTheProgressRow_OnTheFirstStep()
    {
        _repository.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((TutorialProgress?)null);

        await MarkSeen().Handle(new MarkTutorialStepSeenCommand(PlayerId, "intro.collect"), CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<TutorialProgress>(p => p.PlayerId == PlayerId && p.SeenSteps.Contains("intro.collect")),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkSeen_ShouldNotSave_WhenTheStepWasAlreadySeen()
    {
        var progress = new TutorialProgress(Guid.NewGuid(), PlayerId, Now);
        progress.MarkSeen("intro.collect", Now);
        _repository.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(progress);

        await MarkSeen().Handle(new MarkTutorialStepSeenCommand(PlayerId, "intro.collect"), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skip_ShouldCreateTheRow_AndStamp()
    {
        _repository.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((TutorialProgress?)null);

        await Skip().Handle(new SkipTutorialCommand(PlayerId), CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<TutorialProgress>(p => p.PlayerId == PlayerId && p.SkippedAt == Now),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skip_ShouldNotSave_WhenAlreadySkipped()
    {
        var progress = new TutorialProgress(Guid.NewGuid(), PlayerId, Now);
        progress.Skip(Now.AddDays(-1));
        _repository.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(progress);

        await Skip().Handle(new SkipTutorialCommand(PlayerId), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Equal(Now.AddDays(-1), progress.SkippedAt);
    }

    [Fact]
    public async Task GetProgress_ShouldReturnEmpty_WhenThereIsNoRow()
    {
        _repository.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((TutorialProgress?)null);

        var view = await new GetTutorialProgressQueryHandler(_repository)
            .Handle(new GetTutorialProgressQuery(PlayerId), CancellationToken.None);

        Assert.Empty(view.SeenSteps);
        Assert.Null(view.SkippedAt);
    }

    [Theory]
    [InlineData("intro.collect", true)]
    [InlineData("unlock_farm-3", true)]
    [InlineData("Intro.Collect", false)]
    [InlineData(".leading-dot", false)]
    [InlineData("has space", false)]
    [InlineData("", false)]
    public void Validator_ShouldAcceptOnlyTechnicalKeys(string key, bool valid)
    {
        var result = new MarkTutorialStepSeenCommandValidator().Validate(new MarkTutorialStepSeenCommand(PlayerId, key));

        Assert.Equal(valid, result.IsValid);
    }
}
