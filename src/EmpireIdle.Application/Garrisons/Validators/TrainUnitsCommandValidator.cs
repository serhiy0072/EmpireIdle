using EmpireIdle.Application.Garrisons.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Garrisons.Validators
{
    public sealed class TrainUnitsCommandValidator : AbstractValidator<TrainUnitsCommand>
    {
        public TrainUnitsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.UnitType).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).GreaterThan(0);
        }
    }
}
