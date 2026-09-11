using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class SummonHeroCommandValidator : AbstractValidator<SummonHeroCommand>
    {
        public SummonHeroCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.HeroKey).NotEmpty().MaximumLength(50);
        }
    }
}
