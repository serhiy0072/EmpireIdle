using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class OfferResourcesCommandValidator : AbstractValidator<OfferResourcesCommand>
    {
        public OfferResourcesCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Tier).InclusiveBetween(1, 100);
            RuleFor(x => x.Node).NotEmpty().MaximumLength(50);
        }
    }

    public sealed class OfferGemsCommandValidator : AbstractValidator<OfferGemsCommand>
    {
        public OfferGemsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Tier).InclusiveBetween(1, 100);
            RuleFor(x => x.Node).NotEmpty().MaximumLength(50);
            RuleFor(x => x.ExpectedPrice).GreaterThan(0);
        }
    }

    /// <summary>Тір і вузол задаються разом або обидва відсутні — тоді мітку знімають.</summary>
    public sealed class MarkClanTechnologyCommandValidator : AbstractValidator<MarkClanTechnologyCommand>
    {
        public MarkClanTechnologyCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x).Must(x => x.Tier.HasValue == (x.Node is not null))
                .WithMessage("Tier and Node must be given together, or both left out to clear the mark.");
            RuleFor(x => x.Tier).InclusiveBetween(1, 100).When(x => x.Tier.HasValue);
            RuleFor(x => x.Node).NotEmpty().MaximumLength(50).When(x => x.Node is not null);
        }
    }
}
