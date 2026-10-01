using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Exceptions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>Кого можна атакувати: своє й соклановців — ні, решту — так.</summary>
public class HostilityRulesTests
{
    private static readonly Guid Attacker = Guid.NewGuid();
    private static readonly Guid Defender = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();

    private HostilityRules Rules() => new(_clans);

    private void GivenClans(Guid? attackerClan, Guid? defenderClan)
    {
        _clans.GetClanIdByMemberAsync(Attacker, Arg.Any<CancellationToken>()).Returns(attackerClan);
        _clans.GetClanIdByMemberAsync(Defender, Arg.Any<CancellationToken>()).Returns(defenderClan);
    }

    [Theory]
    [InlineData(false, "march.ownVillage")]
    [InlineData(true, "march.ownCamp")]
    public async Task OwnTarget_ShouldBeRefused(bool camp, string reason)
    {
        var refusal = await Rules().RefusalAsync(Attacker, Attacker, camp, CancellationToken.None);

        Assert.Equal(reason, refusal?.Key);
    }

    [Fact]
    public async Task Clanmate_ShouldBeRefused()
    {
        var clan = Guid.NewGuid();
        GivenClans(clan, clan);

        var refusal = await Rules().RefusalAsync(Attacker, Defender, camp: false, CancellationToken.None);

        Assert.Equal(RefusalReasons.MarchClanmate.Key, refusal?.Key);
    }

    [Fact]
    public async Task AnotherClan_ShouldBeAllowed()
    {
        GivenClans(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(await Rules().RefusalAsync(Attacker, Defender, camp: false, CancellationToken.None));
    }

    /// <summary>Обоє поза кланом — не соклановці: null == null тут не рахується.</summary>
    [Fact]
    public async Task BothWithoutClan_ShouldBeAllowed()
    {
        GivenClans(null, null);

        Assert.Null(await Rules().RefusalAsync(Attacker, Defender, camp: false, CancellationToken.None));
    }
}
