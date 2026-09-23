using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Відмови клану, до яких гравець доходить чесною грою. Кожна несе причину:
/// правила рангів різні, але для гравця це одне — «ваша роль цього не дозволяє».
/// </summary>
public class ClanTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeaderId = Guid.NewGuid();

    private static Clan NewClan() => new(Guid.NewGuid(), 1, "Вовки", "WLF", LeaderId, Now);

    private static Guid RoleId(Clan clan, string name) => clan.Roles.Single(r => r.Name == name).Id;

    /// <summary>Новий гравець у ролі, яку видав лідер.</summary>
    private static Guid Recruit(Clan clan, string role)
    {
        var playerId = Guid.NewGuid();
        clan.Join(playerId, capacity: 50, Now);

        if (role != "Member")
            clan.AssignRole(LeaderId, playerId, RoleId(clan, role), Now);

        return playerId;
    }

    [Fact]
    public void Join_AFullClan_ShouldNameTheCapacity()
    {
        var clan = NewClan();

        var refusal = Assert.Throws<RequirementNotMetException>(() => clan.Join(Guid.NewGuid(), capacity: 1, Now));

        Assert.Equal(RefusalReasons.ClanFull.Key, refusal.Reason);
        Assert.Equal(1, refusal.Args["capacity"]);
    }

    [Fact]
    public void Leave_AsTheLeader_ShouldAskToTransferLeadershipFirst()
    {
        var clan = NewClan();

        var refusal = Assert.Throws<InvalidStateException>(() => clan.Leave(LeaderId, Now));

        Assert.Equal(RefusalReasons.ClanLeaderMustTransfer.Key, refusal.Reason);
    }

    /// <summary>Рядовий без права Kick — відмова називає його роль.</summary>
    [Fact]
    public void Kick_WithoutThePermission_ShouldNameTheActorsRole()
    {
        var clan = NewClan();
        var member = Recruit(clan, "Member");
        var other = Recruit(clan, "Member");

        var refusal = Assert.Throws<RequirementNotMetException>(() => clan.Kick(member, other, Now));

        Assert.Equal(RefusalReasons.ClanNoPermission.Key, refusal.Reason);
        Assert.Equal("Member", refusal.Args["role"]);
    }

    /// <summary>Право є, але ціль рівна за рангом — для гравця це та сама відмова ролі.</summary>
    [Fact]
    public void Kick_AnEqualRank_ShouldRefuseAsAMissingPermission()
    {
        var clan = NewClan();
        var officer = Recruit(clan, "Officer");
        var peer = Recruit(clan, "Officer");

        var refusal = Assert.Throws<RequirementNotMetException>(() => clan.Kick(officer, peer, Now));

        Assert.Equal(RefusalReasons.ClanNoPermission.Key, refusal.Reason);
        Assert.Equal("Officer", refusal.Args["role"]);
    }

    [Fact]
    public void TransferLeadership_ByANonLeader_ShouldSayOnlyTheLeaderCan()
    {
        var clan = NewClan();
        var deputy = Recruit(clan, "Deputy");
        var member = Recruit(clan, "Member");

        var refusal = Assert.Throws<RequirementNotMetException>(() => clan.TransferLeadership(deputy, member, Now));

        Assert.Equal(RefusalReasons.ClanLeaderOnly.Key, refusal.Reason);
    }

    /// <summary>Назви ролей порівнюються без регістру: «officer» зайнята так само, як «Officer».</summary>
    [Fact]
    public void CreateRole_WithATakenName_ShouldNameIt()
    {
        var clan = NewClan();

        var refusal = Assert.Throws<AlreadyExistsException>(() =>
            clan.CreateRole(LeaderId, "officer", 30, ClanPermission.None, Now));

        Assert.Equal(RefusalReasons.ClanRoleNameTaken.Key, refusal.Reason);
        Assert.Equal("officer", refusal.Args["name"]);
    }

    [Fact]
    public void UpdateRole_OnTheLeaderRole_ShouldSayItIsProtected()
    {
        var clan = NewClan();

        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            clan.UpdateRole(LeaderId, RoleId(clan, "Leader"), "Boss", 100, ClanPermission.None, Now));

        Assert.Equal(RefusalReasons.ClanRoleProtected.Key, refusal.Reason);
    }
}
