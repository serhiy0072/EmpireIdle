using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class OpenWeaponChestsCommandValidator : AbstractValidator<OpenWeaponChestsCommand>
    {
        public OpenWeaponChestsCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.HeroId).NotEmpty();
            RuleFor(c => c.ItemKey).NotEmpty().MaximumLength(50);
            // +5 потребує 80 шматків — більше за раз відкривати немає сенсу
            RuleFor(c => c.Count).InclusiveBetween(1, 100);
        }
    }
}
