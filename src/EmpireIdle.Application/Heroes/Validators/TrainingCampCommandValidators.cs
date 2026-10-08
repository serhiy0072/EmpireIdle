using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class PlaceHeroInCampCommandValidator : AbstractValidator<PlaceHeroInCampCommand>
    {
        public PlaceHeroInCampCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.HeroId).NotEmpty();
        }
    }

    public sealed class RemoveHeroFromCampCommandValidator : AbstractValidator<RemoveHeroFromCampCommand>
    {
        public RemoveHeroFromCampCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.HeroId).NotEmpty();
        }
    }

    public sealed class SkipCampCooldownCommandValidator : AbstractValidator<SkipCampCooldownCommand>
    {
        public SkipCampCooldownCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.Slot).InclusiveBetween(0, 99);
        }
    }

    public sealed class BuyCampSlotCommandValidator : AbstractValidator<BuyCampSlotCommand>
    {
        public BuyCampSlotCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
        }
    }
}
