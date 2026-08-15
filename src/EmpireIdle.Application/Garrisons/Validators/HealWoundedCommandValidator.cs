using EmpireIdle.Application.Garrisons.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Garrisons.Validators
{
    public sealed class HealWoundedCommandValidator : AbstractValidator<HealWoundedCommand>
    {
        public HealWoundedCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Units).NotEmpty();
            RuleFor(x => x.Payment).IsInEnum();

            RuleForEach(x => x.Units).ChildRules(unit =>
            {
                unit.RuleFor(u => u.Key).NotEmpty().MaximumLength(50);
                unit.RuleFor(u => u.Value).GreaterThan(0);
            });
        }
    }
}
