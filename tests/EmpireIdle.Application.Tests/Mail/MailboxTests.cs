using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Mail.Commands;
using EmpireIdle.Application.Mail.EventHandlers;
using EmpireIdle.Application.Mail.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Mail;

/// <summary>
/// Скринька: лист з'являється з події, показує актуальний стан того, на що
/// посилається, і не розмножує оголошення по адресатах.
/// </summary>
public class MailboxTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IMailRepository _mail = Substitute.For<IMailRepository>();
    private readonly IClanRequestRepository _requests = Substitute.For<IClanRequestRepository>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithBuildings().BuildCatalog();

    public MailboxTests() => _serverContext.ServerId.Returns(1);

    private GetMailboxQueryHandler Mailbox() => new(_mail, _requests, _clans, new FakeTimeProvider(Now));

    // ---------- Лист із події ----------

    [Fact]
    public async Task ClanInvite_ShouldPutALetterReferencingTheRequestAndHighlightIt()
    {
        MailLetter? added = null;
        await _mail.AddLetterAsync(Arg.Do<MailLetter>(l => added = l), Arg.Any<CancellationToken>());
        var requestId = Guid.NewGuid();

        await new ClanInviteMailHandler(_mail, _serverContext, _unitOfWork, _notifier, _catalog).Handle(
            new DomainEventNotification<ClanInviteSent>(new ClanInviteSent(requestId, Guid.NewGuid(), PlayerId, Now.AddDays(3), Now)),
            CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(PlayerId, added.PlayerId);
        Assert.Equal(requestId, added.ReferenceId);
        Assert.Equal(Now.AddDays(_catalog.Config.Mail.LetterRetentionDays), added.ExpiresAt);
        await _notifier.Received(1).NotifyMailAsync(PlayerId, "ClanInvite", Arg.Any<CancellationToken>());
    }

    // ---------- Актуальний стан ----------

    private (MailLetter Letter, ClanRequest Invite) GivenInvite(DateTime expiresAt, bool clanAlive = true)
    {
        var clanId = Guid.NewGuid();
        var invite = new ClanRequest(Guid.NewGuid(), 1, clanId, PlayerId, ClanRequestKind.Invite, expiresAt, Now);
        var letter = new MailLetter(Guid.NewGuid(), 1, PlayerId, MailKind.ClanInvite, invite.Id, Now, TimeSpan.FromDays(14));

        _mail.GetLettersAsync(PlayerId, Now, Arg.Any<CancellationToken>()).Returns([letter]);
        _mail.GetAnnouncementsAsync(Now, Arg.Any<CancellationToken>()).Returns([]);
        _mail.GetReadAnnouncementIdsAsync(PlayerId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
        _requests.GetByIdAsync(invite.Id, Arg.Any<CancellationToken>()).Returns(invite);
        _clans.GetCardAsync(clanId, Arg.Any<CancellationToken>())
            .Returns(clanAlive ? new ClanCard(clanId, "Вовки", "WLF", "", ClanJoinPolicy.Open, 5, Now) : null);

        return (letter, invite);
    }

    [Fact]
    public async Task Mailbox_ShouldOfferButtons_OnlyWhileTheInviteIsPending()
    {
        GivenInvite(Now.AddDays(1));

        var view = await Mailbox().Handle(new GetMailboxQuery(PlayerId), CancellationToken.None);

        var invite = Assert.Single(view.Letters).ClanInvite!;
        Assert.Equal("Pending", invite.State);
        Assert.True(invite.CanRespond);
        Assert.Equal("Вовки", invite.ClanName);
        Assert.Equal(1, view.Unread);
    }

    /// <summary>Лист лишається, але протерміноване запрошення — без кнопок.</summary>
    [Fact]
    public async Task Mailbox_ShouldShowAnExpiredInviteWithoutButtons()
    {
        GivenInvite(Now.AddMinutes(-1));

        var invite = Assert.Single((await Mailbox().Handle(new GetMailboxQuery(PlayerId), CancellationToken.None)).Letters).ClanInvite!;

        Assert.Equal("Expired", invite.State);
        Assert.False(invite.CanRespond);
    }

    [Fact]
    public async Task Mailbox_ShouldShowAnAcceptedInviteWithoutButtons()
    {
        var (_, request) = GivenInvite(Now.AddDays(1));
        request.Accept(PlayerId, Now);

        var invite = Assert.Single((await Mailbox().Handle(new GetMailboxQuery(PlayerId), CancellationToken.None)).Letters).ClanInvite!;

        Assert.Equal("Accepted", invite.State);
        Assert.False(invite.CanRespond);
    }

    /// <summary>Клан розпався — запрошення нікуди не веде.</summary>
    [Fact]
    public async Task Mailbox_ShouldShowAnInviteFromADisbandedClanAsGone()
    {
        GivenInvite(Now.AddDays(1), clanAlive: false);

        var invite = Assert.Single((await Mailbox().Handle(new GetMailboxQuery(PlayerId), CancellationToken.None)).Letters).ClanInvite!;

        Assert.Equal("Gone", invite.State);
        Assert.False(invite.CanRespond);
    }

    // ---------- Прочитання ----------

    [Fact]
    public async Task MarkLetterRead_ShouldRefuse_SomeoneElsesLetter()
    {
        var letter = new MailLetter(Guid.NewGuid(), 1, Guid.NewGuid(), MailKind.ClanInvite, Guid.NewGuid(), Now, TimeSpan.FromDays(1));
        _mail.GetLetterAsync(letter.Id, Arg.Any<CancellationToken>()).Returns(letter);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => new MarkLetterReadCommandHandler(_mail, _unitOfWork, new FakeTimeProvider(Now))
            .Handle(new MarkLetterReadCommand(PlayerId, letter.Id), CancellationToken.None));

        Assert.False(letter.IsRead);
    }

    /// <summary>Перше прочитання лишає свій момент: повторне відкриття його не переписує.</summary>
    [Fact]
    public void MarkRead_ShouldKeepTheFirstMoment()
    {
        var letter = new MailLetter(Guid.NewGuid(), 1, PlayerId, MailKind.ClanInvite, Guid.NewGuid(), Now, TimeSpan.FromDays(1));

        letter.MarkRead(Now.AddMinutes(1));
        letter.MarkRead(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(1), letter.ReadAt);
    }

    /// <summary>Мітка прочитання оголошення створюється один раз — повторне відкриття нічого не додає.</summary>
    [Fact]
    public async Task MarkAnnouncementRead_ShouldNotDuplicateTheMark()
    {
        var announcement = new Announcement(Guid.NewGuid(), 1, AnnouncementKind.News, "Оновлення", "Текст", Now, Now.AddDays(1));
        _mail.GetAnnouncementAsync(announcement.Id, Arg.Any<CancellationToken>()).Returns(announcement);
        _mail.IsAnnouncementReadAsync(announcement.Id, PlayerId, Arg.Any<CancellationToken>()).Returns(true);

        await new MarkAnnouncementReadCommandHandler(_mail, _unitOfWork, new FakeTimeProvider(Now))
            .Handle(new MarkAnnouncementReadCommand(PlayerId, announcement.Id), CancellationToken.None);

        await _mail.DidNotReceive().AddAnnouncementReadAsync(Arg.Any<AnnouncementRead>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Оголошення публікується одним рядком на світ і підсвічується всім у світі.</summary>
    [Fact]
    public async Task Publish_ShouldStoreOneAnnouncementAndNotifyTheWorld()
    {
        Announcement? added = null;
        await _mail.AddAnnouncementAsync(Arg.Do<Announcement>(a => added = a), Arg.Any<CancellationToken>());

        await new PublishAnnouncementCommandHandler(_mail, _serverContext, _unitOfWork, _notifier, _catalog, new FakeTimeProvider(Now))
            .Handle(new PublishAnnouncementCommand(AnnouncementKind.Maintenance, " Техроботи ", "О 03:00", null), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal("Техроботи", added.Title);
        Assert.Equal(Now.AddDays(_catalog.Config.Mail.AnnouncementRetentionDays), added.ExpiresAt);
        await _notifier.Received(1).NotifyAnnouncementAsync(1, Arg.Any<CancellationToken>());
    }
}
