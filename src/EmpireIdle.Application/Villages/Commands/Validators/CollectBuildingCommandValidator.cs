using FluentValidation;

namespace EmpireIdle.Application.Villages.Commands.Validators
{
    public sealed class CollectBuildingCommandValidator : AbstractValidator<CollectBuildingCommand>
    {
        public CollectBuildingCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.BuildingId).NotEmpty();
        }
    }
}
