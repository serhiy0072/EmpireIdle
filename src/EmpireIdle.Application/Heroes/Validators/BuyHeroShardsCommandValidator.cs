using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class BuyHeroShardsCommandValidator : AbstractValidator<BuyHeroShardsCommand>
    {
        public BuyHeroShardsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.HeroKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).GreaterThan(0);
        }
    }
}
