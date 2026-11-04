using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class OfferResourcesCommandValidator : AbstractValidator<OfferResourcesCommand>
    {
        public OfferResourcesCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Resource).NotEmpty().MaximumLength(50);
        }
    }

    public sealed class OfferGemsCommandValidator : AbstractValidator<OfferGemsCommand>
    {
        public OfferGemsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ExpectedPrice).GreaterThan(0);
        }
    }
}
