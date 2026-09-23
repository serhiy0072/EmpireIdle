using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Допомога клану: запит протухає, один гравець допомагає раз, і є стеля
/// допомог. Кожна з цих відмов — нормальний перебіг гри, тож із причиною.
/// </summary>
public class ClanHelpRequestTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private static ClanHelpRequest Request(DateTime? expiresAt = null)
        => new(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), ClanHelpTarget.Construction, Guid.NewGuid(),
            TimeSpan.FromHours(1), expiresAt ?? Now.AddHours(1), Now);

    [Fact]
    public void AcceptHelp_AfterExpiry_ShouldSayTheRequestExpired()
    {
        var request = Request(expiresAt: Now);

        var refusal = Assert.Throws<InvalidStateException>(() => request.AcceptHelp(Guid.NewGuid(), 0.02, 20, Now));

        Assert.Equal(RefusalReasons.ClanHelpExpired.Key, refusal.Reason);
    }

    [Fact]
    public void AcceptHelp_Twice_ShouldSayTheHelperAlreadyHelped()
    {
        var request = Request();
        var helper = Guid.NewGuid();
        request.AcceptHelp(helper, 0.02, 20, Now);

        var refusal = Assert.Throws<AlreadyExistsException>(() => request.AcceptHelp(helper, 0.02, 20, Now));

        Assert.Equal(RefusalReasons.ClanHelpAlreadyHelped.Key, refusal.Reason);
    }

    [Fact]
    public void AcceptHelp_PastTheCap_ShouldNameTheCap()
    {
        var request = Request();
        request.AcceptHelp(Guid.NewGuid(), 0.02, maxHelpers: 1, Now);

        var refusal = Assert.Throws<InvalidStateException>(() => request.AcceptHelp(Guid.NewGuid(), 0.02, maxHelpers: 1, Now));

        Assert.Equal(RefusalReasons.ClanHelpFull.Key, refusal.Reason);
        Assert.Equal(1, refusal.Args["max"]);
    }
}
