using EmpireIdle.Application.Inventory.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Inventory.Validators
{
    public sealed class UseItemCommandValidator : AbstractValidator<UseItemCommand>
    {
        public UseItemCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).GreaterThan(0);


            // Координати задаються парою: одна половина означає помилку клієнта,
            // і краще сказати про це одразу, ніж дати ефекту впасти на TargetX is not { }
            RuleFor(x => x)
                .Must(x => x.TargetX.HasValue == x.TargetY.HasValue)
                .WithMessage("TargetX and TargetY must be provided together.");
        }
    }
}
