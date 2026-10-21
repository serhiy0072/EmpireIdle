using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class FeedArtifactCommandValidator : AbstractValidator<FeedArtifactCommand>
    {
        public FeedArtifactCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.EquipmentId).NotEmpty();
            RuleFor(c => c.FoodIds).NotNull();
            RuleFor(c => c.Wrenches).NotNull();

            // Порожнє згодовування нічого не змінює — це помилка клієнта, а не відмова гри
            RuleFor(c => c)
                .Must(c => c.FoodIds.Count > 0 || c.Wrenches.Count > 0)
                .When(c => c.FoodIds is not null && c.Wrenches is not null)
                .WithMessage("Nothing to feed.");

            RuleFor(c => c.FoodIds.Count).LessThanOrEqualTo(FeedArtifactCommand.MaxFood).When(c => c.FoodIds is not null);
            RuleForEach(c => c.FoodIds).NotEmpty();
            RuleForEach(c => c.Wrenches).ChildRules(w =>
            {
                w.RuleFor(p => p.Key).NotEmpty().MaximumLength(50);
                w.RuleFor(p => p.Value).InclusiveBetween(1, 10_000);
            });
        }
    }
}
