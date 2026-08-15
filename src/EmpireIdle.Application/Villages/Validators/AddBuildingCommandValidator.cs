using EmpireIdle.Application.Villages.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Villages.Validators
{
    public sealed class AddBuildingCommandValidator : AbstractValidator<AddBuildingCommand>
    {
        public AddBuildingCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.BuildingType).NotEmpty().MaximumLength(50);
        }
    }
}
