using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class SelectClanTechnologyCommandValidator : AbstractValidator<SelectClanTechnologyCommand>
    {
        public SelectClanTechnologyCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Tier).InclusiveBetween(1, 100);
            RuleFor(x => x.Node).NotEmpty().MaximumLength(50);
        }
    }
}
