using EmpireIdle.Application.Territory.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Territory.Validators
{
    public sealed class RecallFromStructureCommandValidator : AbstractValidator<RecallFromStructureCommand>
    {
        public RecallFromStructureCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.StructureId).NotEmpty();
        }
    }
}
