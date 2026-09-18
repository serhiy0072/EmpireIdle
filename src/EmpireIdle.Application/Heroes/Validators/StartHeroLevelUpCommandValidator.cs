using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class StartHeroLevelUpCommandValidator : AbstractValidator<StartHeroLevelUpCommand>
    {
        public StartHeroLevelUpCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.HeroId).NotEmpty();
        }
    }
}
