using EmpireIdle.Application.Chat.Commands;
using EmpireIdle.Application.Chat.Contracts;
using EmpireIdle.Application.Chat.EventHandlers;
using EmpireIdle.Application.Chat.Queries;
using EmpireIdle.Application.Chat.Services;
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Chat;

/// <summary>
/// Чат: хто куди може писати, антиспам, історія читача з перекладами,
/// доставка з перекладами на мови світу.
/// </summary>
public class ChatCommandTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;

    private readonly IChatRepository _chat = Substitute.For<IChatRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();
    private readonly ITranslator _translator = Substitute.For<ITranslator>();
    private readonly List<ChatMessage> _added = [];

    private readonly GameCatalog _catalog;

    public ChatCommandTests()
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Chat.MaxLength = 20;
        config.Chat.RateLimitCount = 3;
        config.Chat.RateLimitWindowSeconds = 10;
        config.Localization.Languages = ["uk", "en"];

        _catalog = new GameCatalog(config);
        _serverContext.ServerId.Returns(1);
        _chat.AddAsync(Arg.Do<ChatMessage>(_added.Add), Arg.Any<CancellationToken>());
    }

    private SendChatMessageCommandHandler Send() => new(_chat, _players, _serverContext, _catalog, _unitOfWork,
        new FakeTimeProvider(Now), NullLogger<SendChatMessageCommandHandler>.Instance);

    private Player GivenPlayer(Guid? clanId = null, string language = "uk")
    {
        var player = new Player(Guid.NewGuid(), $"p{Guid.NewGuid():N}"[..8], "p@x.com", Guid.NewGuid().ToString(), Now, 1, language);

        if (clanId is { } clan)
            player.JoinClan(clan);

        _players.GetByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
        _players.GetNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<IReadOnlyCollection<Guid>>().ToDictionary(id => id, id => id.ToString()[..4]));

        return player;
    }

    // ---------- Надсилання ----------

    [Fact]
    public async Task Send_ShouldStoreATrimmedMessageInTheSendersLanguage()
    {
        var sender = GivenPlayer(language: "en");

        await Send().Handle(new SendChatMessageCommand(sender.Id, ChatChannel.Server, null, "  hello  "), CancellationToken.None);

        var message = Assert.Single(_added);
        Assert.Equal("hello", message.Text);
        Assert.Equal("en", message.Language);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_ShouldRefuse_ATooLongMessage()
    {
        var sender = GivenPlayer();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Send().Handle(new SendChatMessageCommand(sender.Id, ChatChannel.Server, null, new string('a', 21)), CancellationToken.None));

        Assert.Equal(RefusalReasons.ChatTooLong.Key, refusal.Reason);
        Assert.Equal(20, refusal.Args["max"]);
    }

    /// <summary>Три повідомлення за вікно вже є — четверте чекає, поки найстаріше вийде з вікна.</summary>
    [Fact]
    public async Task Send_ShouldRefuse_WhenFlooding()
    {
        var sender = GivenPlayer();
        _chat.CountSentSinceAsync(sender.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(3);
        _chat.GetOldestSentSinceAsync(sender.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(Now.AddSeconds(-4));

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Send().Handle(new SendChatMessageCommand(sender.Id, ChatChannel.Server, null, "spam"), CancellationToken.None));

        Assert.Equal(RefusalReasons.ChatTooFast.Key, refusal.Reason);
        Assert.Equal(6, refusal.Args["seconds"]);
        Assert.Empty(_added);
    }

    /// <summary>Клан — з бази: гравець без клану в клановий канал не пише.</summary>
    [Fact]
    public async Task Send_ShouldRefuse_AClanMessageWithoutAClan()
    {
        var sender = GivenPlayer();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Send().Handle(new SendChatMessageCommand(sender.Id, ChatChannel.Clan, null, "hi"), CancellationToken.None));

        Assert.Equal(RefusalReasons.ChatNoClan.Key, refusal.Reason);
    }

    [Fact]
    public async Task Send_ShouldAddressAClanMessageToTheSendersClan()
    {
        var clan = Guid.NewGuid();
        var sender = GivenPlayer(clan);

        await Send().Handle(new SendChatMessageCommand(sender.Id, ChatChannel.Clan, null, "hi"), CancellationToken.None);

        Assert.Equal(clan, Assert.Single(_added).ClanId);
    }

    [Fact]
    public async Task Send_ShouldRefuse_APrivateMessageToSelf()
    {
        var sender = GivenPlayer();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Send().Handle(new SendChatMessageCommand(sender.Id, ChatChannel.Private, sender.Id, "hi"), CancellationToken.None));

        Assert.Equal(RefusalReasons.ChatToSelf.Key, refusal.Reason);
    }

    /// <summary>Адресат з іншого світу чи неіснуючий — 404, а не лист у порожнечу.</summary>
    [Fact]
    public async Task Send_ShouldRefuse_AnUnknownRecipient()
    {
        var sender = GivenPlayer();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Send().Handle(new SendChatMessageCommand(sender.Id, ChatChannel.Private, Guid.NewGuid(), "hi"), CancellationToken.None));
    }

    // ---------- Історія ----------

    /// <summary>Читач бачить переклад повідомлення іншою мовою, якщо він у кеші; своєю — без перекладу.</summary>
    [Fact]
    public async Task History_ShouldAttachCachedTranslationsForTheReader()
    {
        var reader = GivenPlayer(language: "uk");
        var english = new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Server, null, Guid.NewGuid(), null, "hello", "en", Now);
        var ukrainian = new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Server, null, reader.Id, null, "привіт", "uk", Now);

        _chat.GetServerHistoryAsync(null, 50, Arg.Any<CancellationToken>()).Returns([english, ukrainian]);
        _chat.GetTranslationsAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == english.Id), "uk", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [english.Id] = "привіт" });

        var handler = new GetChatHistoryQueryHandler(_chat, _players, new ChatProjection(_chat, _players));

        var history = await handler.Handle(new GetChatHistoryQuery(reader.Id, ChatChannel.Server, null, null, 50), CancellationToken.None);

        Assert.Equal("привіт", history[0].TranslatedText);
        Assert.Null(history[1].TranslatedText);
        Assert.True(history[1].IsOwn);
    }

    /// <summary>Без клану клановий канал — порожній, а не помилка.</summary>
    [Fact]
    public async Task History_ShouldBeEmpty_ForTheClanChannelWithoutAClan()
    {
        var reader = GivenPlayer();
        var handler = new GetChatHistoryQueryHandler(_chat, _players, new ChatProjection(_chat, _players));

        var history = await handler.Handle(new GetChatHistoryQuery(reader.Id, ChatChannel.Clan, null, null, 50), CancellationToken.None);

        Assert.Empty(history);
    }

    // ---------- Доставка ----------

    private ChatMessageSentHandler Deliver() => new(_chat, _players, _translator, _notifier, _unitOfWork, _catalog,
        NullLogger<ChatMessageSentHandler>.Instance);

    /// <summary>Кланове повідомлення йде тим, хто в клані зараз, — за даними з бази.</summary>
    [Fact]
    public async Task Deliver_ShouldSendAClanMessageToCurrentMembers()
    {
        var clan = Guid.NewGuid();
        var sender = GivenPlayer(clan);
        var message = new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Clan, clan, sender.Id, null, "hi", "uk", Now);
        IReadOnlyList<Guid> members = [sender.Id, Guid.NewGuid()];

        _chat.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _players.GetIdsByClanAsync(clan, Arg.Any<CancellationToken>()).Returns(members);

        await Deliver().Handle(new DomainEventNotification<ChatMessageSent>(
            new ChatMessageSent(message.Id, 1, ChatChannel.Clan, clan, sender.Id, null, Now)), CancellationToken.None);

        await _notifier.Received(1).NotifyChatToPlayersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(members)),
            Arg.Is<ChatMessageNotice>(n => n.Id == message.Id), Arg.Any<CancellationToken>());
    }

    /// <summary>З провайдером повідомлення перекладається на інші мови світу й кешується.</summary>
    [Fact]
    public async Task Deliver_ShouldTranslateToTheOtherLanguagesAndCacheIt()
    {
        var sender = GivenPlayer();
        var message = new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Server, null, sender.Id, null, "привіт", "uk", Now);

        _chat.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _translator.IsAvailable.Returns(true);
        _translator.TranslateAsync("привіт", "uk", "en", Arg.Any<CancellationToken>()).Returns("hello");

        await Deliver().Handle(new DomainEventNotification<ChatMessageSent>(
            new ChatMessageSent(message.Id, 1, ChatChannel.Server, null, sender.Id, null, Now)), CancellationToken.None);

        await _chat.Received(1).AddTranslationAsync(Arg.Is<ChatTranslation>(t => t.Language == "en" && t.Text == "hello"),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyChatToServerAsync(1,
            Arg.Is<ChatMessageNotice>(n => n.Translations["en"] == "hello"), Arg.Any<CancellationToken>());
    }

    /// <summary>Збій перекладача не зупиняє доставку: піде оригінал.</summary>
    [Fact]
    public async Task Deliver_ShouldStillDeliver_WhenTheTranslatorFails()
    {
        var sender = GivenPlayer();
        var message = new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Server, null, sender.Id, null, "привіт", "uk", Now);

        _chat.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _translator.IsAvailable.Returns(true);
        _translator.TranslateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string?>(_ => throw new HttpRequestException("provider down"));

        await Deliver().Handle(new DomainEventNotification<ChatMessageSent>(
            new ChatMessageSent(message.Id, 1, ChatChannel.Server, null, sender.Id, null, Now)), CancellationToken.None);

        await _notifier.Received(1).NotifyChatToServerAsync(1,
            Arg.Is<ChatMessageNotice>(n => n.Translations.Count == 0), Arg.Any<CancellationToken>());
    }

    // ---------- Мова ----------

    [Fact]
    public async Task ChangeLanguage_ShouldSwitchToASupportedLanguage()
    {
        var player = GivenPlayer();

        await new ChangeLanguageCommandHandler(_players, _catalog, _unitOfWork)
            .Handle(new ChangeLanguageCommand(player.Id, "en"), CancellationToken.None);

        Assert.Equal("en", player.Language);
    }

    [Fact]
    public async Task ChangeLanguage_ShouldRefuse_AnUnsupportedLanguage()
    {
        var player = GivenPlayer();

        await Assert.ThrowsAsync<RequirementNotMetException>(() => new ChangeLanguageCommandHandler(_players, _catalog, _unitOfWork)
            .Handle(new ChangeLanguageCommand(player.Id, "de"), CancellationToken.None));

        Assert.Equal("uk", player.Language);
    }
}
