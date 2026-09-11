using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

public class SummonHeroCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private SummonHeroCommandHandler Handler()
    {
        _serverContext.ServerId.Returns(ServerId);

        var granter = new HeroGranter(_heroes, _players, _wallets, _serverContext, HeroTestConfig.Catalog());

        return new SummonHeroCommandHandler(
            _heroes, granter, _unitOfWork, new FakeTimeProvider(Now),
            NullLogger<SummonHeroCommandHandler>.Instance, HeroTestConfig.Catalog());
    }

    private HeroShardProgress GivenShards(int count)
    {
        var progress = new HeroShardProgress(Guid.NewGuid(), PlayerId, ServerId, "warrior_bran");

        if (count > 0)
            progress.Add(count);

        _heroes.GetShardsAsync(PlayerId, "warrior_bran", Arg.Any<CancellationToken>()).Returns(progress);

        return progress;
    }

    /// <summary>Поріг набраний — герой з'являється в ростері.</summary>
    [Fact]
    public async Task Handle_ShouldAddHero_WhenShardsSuffice()
    {
        GivenShards(10);

        await Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None);

        await _heroes.Received(1).AddAsync(
            Arg.Is<Hero>(h => h.HeroKey == "warrior_bran" && h.Level == 1 && h.Tier == 1),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Списується рівно поріг, надлишок лишається гравцю.</summary>
    [Fact]
    public async Task Handle_ShouldSpendExactlyTheThreshold()
    {
        var progress = GivenShards(13);

        await Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None);

        Assert.Equal(3, progress.Count);
    }

    /// <summary>
    /// Нижче порогу — відмова без списання. Часткове зняття з'їло б
    /// уже куплені уламки.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_AndKeepShards_WhenBelowTheThreshold()
    {
        var progress = GivenShards(9);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None));

        Assert.Equal(9, progress.Count);
        await _heroes.DidNotReceive().AddAsync(Arg.Any<Hero>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Жодного уламка — окреме повідомлення, не нульовий призов.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenNoShardsCollected()
    {
        _heroes.GetShardsAsync(PlayerId, "warrior_bran", Arg.Any<CancellationToken>())
            .Returns((HeroShardProgress?)null);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None));
    }

    /// <summary>
    /// Повторний призов наявного героя йде в сузір'я, а не другим рядком —
    /// це те саме правило, що тримає унікальний індекс у базі.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldRaiseConstellation_WhenTheHeroIsAlreadyOwned()
    {
        GivenShards(10);

        var owned = new Hero(Guid.NewGuid(), PlayerId, ServerId, "warrior_bran", Now);
        _heroes.GetByKeyAsync(PlayerId, "warrior_bran", Arg.Any<CancellationToken>()).Returns(owned);

        await Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None);

        Assert.Equal(1, owned.Constellation);
        await _heroes.DidNotReceive().AddAsync(Arg.Any<Hero>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Невідомий герой — 404, а не 500.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownHero()
        => await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new SummonHeroCommand(PlayerId, "dragon_rider"), CancellationToken.None));
}
