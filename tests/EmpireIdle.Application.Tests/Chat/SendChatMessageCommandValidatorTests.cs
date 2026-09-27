using EmpireIdle.Application.Chat.Commands;
using EmpireIdle.Application.Chat.Validators;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Tests.Chat;

/// <summary>
/// Форма повідомлення: керівні символи ламають розмітку чату й логи, а NUL Postgres
/// у text не приймає — такий запит мав би впасти 500 на вставці замість 400.
/// </summary>
public class SendChatMessageCommandValidatorTests
{
    private static bool IsValid(string text)
        => new SendChatMessageCommandValidator()
            .Validate(new SendChatMessageCommand(Guid.NewGuid(), ChatChannel.Server, null, text))
            .IsValid;

    [Theory]
    [InlineData("hi\0there")]
    [InlineData("bell\u0007")]
    [InlineData("esc\u001B[31mred")]
    [InlineData("del\u007F")]
    [InlineData("back\rspace")]
    public void Validate_ShouldReject_ControlCharacters(string text)
        => Assert.False(IsValid(text));

    [Theory]
    [InlineData("line one\nline two")]
    [InlineData("col\tcol")]
    [InlineData("Привіт, світе! 👋")]
    public void Validate_ShouldAccept_NewlinesTabsAndUnicode(string text)
        => Assert.True(IsValid(text));
}
