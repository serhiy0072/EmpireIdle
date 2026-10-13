using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class ConvertUniversalShardsCommandValidator : AbstractValidator<ConvertUniversalShardsCommand>
    {
        public ConvertUniversalShardsCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.HeroKey).NotEmpty().MaximumLength(50);
            RuleFor(c => c.Count).InclusiveBetween(1, 10_000);
        }
    }

    public sealed class UpgradeUniversalShardsCommandValidator : AbstractValidator<UpgradeUniversalShardsCommand>
    {
        public UpgradeUniversalShardsCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.From).IsInEnum();
            RuleFor(c => c.Count).InclusiveBetween(1, 1_000);
        }

    public sealed class UpgradeHeroSkillCommandValidator : AbstractValidator<UpgradeHeroSkillCommand>
    {
        public UpgradeHeroSkillCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.HeroId).NotEmpty();
            RuleFor(c => c.SkillKey).NotEmpty().MaximumLength(80);
        }
    }
    }
}
