using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class BuyWeaponCommandValidator : AbstractValidator<BuyWeaponCommand>
    {
        public BuyWeaponCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
        }
    }
}
