using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Повідомлення чату: канал і адресат узгоджені, подія для доставки піднімається.</summary>
public class ChatMessageTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;

    [Fact]
    public void Constructor_ShouldRaiseTheEventForDelivery()
    {
        var clanId = Guid.NewGuid();

        var message = new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Clan, clanId, Guid.NewGuid(), null, "привіт", "uk", Now);

        var sent = Assert.IsType<ChatMessageSent>(Assert.Single(message.DomainEvents));
        Assert.Equal(clanId, sent.ClanId);
        Assert.Equal(ChatChannel.Clan, sent.Channel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldRejectAnEmptyText(string text)
        => Assert.Throws<ArgumentException>(() =>
            new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Server, null, Guid.NewGuid(), null, text, "uk", Now));

    /// <summary>Канал і адресат мусять збігатись — інакше це баг викликача.</summary>
    [Fact]
    public void Constructor_ShouldRejectAMismatchedChannelAndTarget()
    {
        Assert.Throws<ArgumentException>(() =>
            new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Clan, null, Guid.NewGuid(), null, "x", "uk", Now));
        Assert.Throws<ArgumentException>(() =>
            new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Server, Guid.NewGuid(), Guid.NewGuid(), null, "x", "uk", Now));
        Assert.Throws<ArgumentException>(() =>
            new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Private, null, Guid.NewGuid(), null, "x", "uk", Now));
        Assert.Throws<ArgumentException>(() =>
            new ChatMessage(Guid.NewGuid(), 1, ChatChannel.Server, null, Guid.NewGuid(), Guid.NewGuid(), "x", "uk", Now));
    }
}
