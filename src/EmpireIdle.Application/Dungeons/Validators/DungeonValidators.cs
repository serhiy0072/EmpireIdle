using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Domain.Services;
using FluentValidation;

namespace EmpireIdle.Application.Dungeons.Validators
{
    public sealed class StartDungeonRunCommandValidator : AbstractValidator<StartDungeonRunCommand>
    {
        public StartDungeonRunCommandValidator(GameCatalog catalog)
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.DungeonKey).NotEmpty().MaximumLength(50);
            // Межа рівня — з конфіга: піднятий MaxLevel інакше відбивав би нові рівні як сміття
            RuleFor(x => x.Level).InclusiveBetween(1, catalog.Config.Dungeons.MaxLevel);
            RuleFor(x => x.HeroIds).NotEmpty();
            RuleForEach(x => x.HeroIds).NotEmpty();
        }
    }

    public sealed class TakeDungeonTurnCommandValidator : AbstractValidator<TakeDungeonTurnCommand>
    {
        public TakeDungeonTurnCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.RunId).NotEmpty();
            RuleFor(x => x.ExpectedTurn).GreaterThanOrEqualTo(0);
            RuleFor(x => x.AbilityKey).MaximumLength(50);
            RuleFor(x => x.TargetIndex).GreaterThanOrEqualTo(0).When(x => x.TargetIndex.HasValue);
        }
    }

    public sealed class AbandonDungeonRunCommandValidator : AbstractValidator<AbandonDungeonRunCommand>
    {
        public AbandonDungeonRunCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.RunId).NotEmpty();
        }
    }
}
