using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class EvolveHeroTierCommandValidator : AbstractValidator<EvolveHeroTierCommand>
    {
        public EvolveHeroTierCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.HeroId).NotEmpty();
        }
    }
}
