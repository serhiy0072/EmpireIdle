using EmpireIdle.Application.Shop.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Shop.Validators
{
    public sealed class BuyHeroWeaponShardsCommandValidator : AbstractValidator<BuyHeroWeaponShardsCommand>
    {
        public BuyHeroWeaponShardsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.HeroId).NotEmpty();
            // +5 — це 80 шматків; більше за раз купувати немає сенсу
            RuleFor(x => x.Count).InclusiveBetween(1, 100);
        }
    }
}
