using FluentValidation;

namespace EmpireIdle.Application.Villages.Commands.Validators
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
