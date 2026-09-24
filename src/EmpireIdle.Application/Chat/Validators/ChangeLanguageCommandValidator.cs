using EmpireIdle.Application.Chat.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Chat.Validators
{
    public sealed class ChangeLanguageCommandValidator : AbstractValidator<ChangeLanguageCommand>
    {
        public ChangeLanguageCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Language).NotEmpty().MaximumLength(8);
        }
    }
}
