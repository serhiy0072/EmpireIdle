using EmpireIdle.Application.Research.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Research.Validators
{
    public sealed class StartResearchCommandValidator : AbstractValidator<StartResearchCommand>
    {
        public StartResearchCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Tier).InclusiveBetween(1, 100);
            RuleFor(x => x.Node).NotEmpty().MaximumLength(50);
        }
    }
}
