using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class BuyHeroShardsCommandValidator : AbstractValidator<BuyHeroShardsCommand>
    {
        /// <summary>
        /// Стеля однієї покупки. Покриває повний шлях героя (призов і 6 сузір'їв = 70 уламків)
        /// і тримає добуток ціни на кількість далеко від межі int.
        /// </summary>
        public const int MaxPerPurchase = 100;

        public BuyHeroShardsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.HeroKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).InclusiveBetween(1, MaxPerPurchase);
        }
    }
}
