using EmpireIdle.Application.Tower.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Tower.Validators
{
    public sealed class StormTowerFloorCommandValidator : AbstractValidator<StormTowerFloorCommand>
    {
        public StormTowerFloorCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Floor).GreaterThanOrEqualTo(1);
            // Точний розмір загону знає конфіг — тут лише захист від сміття
            RuleFor(x => x.Lanes).NotEmpty().Must(lanes => lanes.Count <= 10).Must(lanes => lanes.Any(id => id is not null))
                .WithMessage("A squad needs at least one hero on at most 10 lanes.");
        }
    }
}
