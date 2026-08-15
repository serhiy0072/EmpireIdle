using EmpireIdle.Application.Villages.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Villages.Validators
{
    public sealed class UpgradeBuildingCommandValidator : AbstractValidator<UpgradeBuildingCommand>
    {
        public UpgradeBuildingCommandValidator()
        {
            RuleFor(x=>x.PlayerId).NotEmpty();
            RuleFor(x=>x.BuildingId).NotEmpty();
        }
    }
}
