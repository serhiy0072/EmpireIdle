using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Validators;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>
/// Власні ролі клану (GDD §7.1): створення, зміна й видалення. Правила рангів і дозволів перевіряє агрегат —
/// тут те, що обробники доводять команду до нього, беруть стелю з конфігу й зберігають зміни.
/// </summary>
public class ClanRoleCommandTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeaderId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithBuildings().BuildCatalog();

    private Clan GivenClan()
    {
        var clan = new Clan(Guid.NewGuid(), 1, "Вовки", "WLF", LeaderId, Now.AddDays(-30));
        _clans.GetByMemberAsync(LeaderId, Arg.Any<CancellationToken>()).Returns(clan);

        return clan;
    }

    private Task<Guid> Create(string name, int rank, ClanPermission permissions, Guid? actor = null)
        => new CreateClanRoleCommandHandler(_clans, _unitOfWork, _catalog, new FakeTimeProvider(Now),
                NullLogger<CreateClanRoleCommandHandler>.Instance)
            .Handle(new CreateClanRoleCommand(actor ?? LeaderId, name, rank, permissions), CancellationToken.None);

    [Fact]
    public async Task Create_ShouldAddTheRole_AndSave()
    {
        var clan = GivenClan();

        var roleId = await Create("  Scouts ", 20, ClanPermission.Recruit);

        var role = clan.Roles.Single(r => r.Id == roleId);
        Assert.Equal(("Scouts", 20, ClanPermission.Recruit, null), (role.Name, role.Rank, role.Permissions, role.Key));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Стеля ролей — з конфігу клану, а не з коду.</summary>
    [Fact]
    public async Task Create_ShouldRefuse_BeyondTheConfiguredLimit()
    {
        GivenClan();
        _catalog.Config.Clan.MaxRoles = 6;

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Create("Scouts", 20, ClanPermission.None));

        Assert.Equal(RefusalReasons.ClanRolesLimit.Key, refusal.Reason);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ShouldRefuse_APlayerWithoutAClan()
    {
        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Create("Scouts", 20, ClanPermission.None, actor: Guid.NewGuid()));

        Assert.Equal(RefusalReasons.ClanNotMember.Key, refusal.Reason);
    }

    [Fact]
    public async Task Update_ShouldChangeTheRole_AndSave()
    {
        var clan = GivenClan();
        var veteran = clan.Roles.Single(r => r.Key == "veteran");

        await new UpdateClanRoleCommandHandler(_clans, _unitOfWork, new FakeTimeProvider(Now),
                NullLogger<UpdateClanRoleCommandHandler>.Instance)
            .Handle(new UpdateClanRoleCommand(LeaderId, veteran.Id, "Old Guard", 25, ClanPermission.Recruit), CancellationToken.None);

        Assert.Equal(("Old Guard", 25, ClanPermission.Recruit, null), (veteran.Name, veteran.Rank, veteran.Permissions, veteran.Key));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ShouldRemoveTheRole_AndSave()
    {
        var clan = GivenClan();
        var veteran = clan.Roles.Single(r => r.Key == "veteran");

        await new DeleteClanRoleCommandHandler(_clans, _unitOfWork, new FakeTimeProvider(Now),
                NullLogger<DeleteClanRoleCommandHandler>.Instance)
            .Handle(new DeleteClanRoleCommand(LeaderId, veteran.Id), CancellationToken.None);

        Assert.DoesNotContain(clan.Roles, r => r.Id == veteran.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Валідатори ----------

    [Theory]
    [InlineData("Scouts", 20, ClanPermission.Recruit | ClanPermission.Kick, true)]
    [InlineData("", 20, ClanPermission.None, false)]
    [InlineData("A name far longer than the column allows", 20, ClanPermission.None, false)]
    [InlineData("Scouts", -1, ClanPermission.None, false)]
    [InlineData("Scouts", 101, ClanPermission.None, false)]
    [InlineData("Scouts", 20, (ClanPermission)4096, false)]
    public void Validators_ShouldBoundTheNameRankAndPermissions(string name, int rank, ClanPermission permissions, bool valid)
    {
        Assert.Equal(valid, new CreateClanRoleCommandValidator()
            .Validate(new CreateClanRoleCommand(LeaderId, name, rank, permissions)).IsValid);
        Assert.Equal(valid, new UpdateClanRoleCommandValidator()
            .Validate(new UpdateClanRoleCommand(LeaderId, Guid.NewGuid(), name, rank, permissions)).IsValid);
    }

    [Fact]
    public void DeleteValidator_ShouldRequireTheRole()
        => Assert.False(new DeleteClanRoleCommandValidator().Validate(new DeleteClanRoleCommand(LeaderId, Guid.Empty)).IsValid);
}
