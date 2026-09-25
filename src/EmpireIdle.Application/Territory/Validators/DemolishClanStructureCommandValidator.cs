using EmpireIdle.Application.Territory.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Territory.Validators
{
    public sealed class DemolishClanStructureCommandValidator : AbstractValidator<DemolishClanStructureCommand>
    {
        public DemolishClanStructureCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.StructureId).NotEmpty();
        }
    }
}
