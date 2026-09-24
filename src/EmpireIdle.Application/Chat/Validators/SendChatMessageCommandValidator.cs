using EmpireIdle.Application.Chat.Commands;
using EmpireIdle.Domain.Enums;
using FluentValidation;

namespace EmpireIdle.Application.Chat.Validators
{
    /// <summary>
    /// Форма повідомлення. Межа довжини для гравця — у конфігу й перевіряється
    /// обробником з причиною; тут лише жорстка стеля, що не пустить мегабайт у базу.
    /// </summary>
    public sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
    {
        public const int HardMaxLength = 2000;

        public SendChatMessageCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Channel).IsInEnum();
            RuleFor(x => x.Text).NotEmpty().MaximumLength(HardMaxLength).Must(text => !string.IsNullOrWhiteSpace(text));

            When(x => x.Channel == ChatChannel.Private, () => RuleFor(x => x.RecipientId).NotNull().NotEqual(Guid.Empty));
            When(x => x.Channel != ChatChannel.Private, () => RuleFor(x => x.RecipientId).Null());
        }
    }
}
