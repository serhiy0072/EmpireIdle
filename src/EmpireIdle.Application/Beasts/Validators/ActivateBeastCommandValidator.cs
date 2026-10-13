using EmpireIdle.Application.Beasts.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Beasts.Validators
{
    public sealed class ActivateBeastCommandValidator : AbstractValidator<ActivateBeastCommand>
    {
        public ActivateBeastCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.BeastKey).NotEmpty().MaximumLength(50);
        }
    }
}
