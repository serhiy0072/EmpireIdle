using EmpireIdle.Application.Territory.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Territory.Validators
{
    public sealed class PlaceClanStructureCommandValidator : AbstractValidator<PlaceClanStructureCommand>
    {
        public PlaceClanStructureCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.X).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Y).GreaterThanOrEqualTo(0);
        }
    }
}
