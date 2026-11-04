using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class AllocateClanPointsCommandValidator : AbstractValidator<AllocateClanPointsCommand>
    {
        public AllocateClanPointsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Tier).InclusiveBetween(1, 100);
            RuleFor(x => x.Node).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Points).GreaterThan(0);
        }
    }
}
