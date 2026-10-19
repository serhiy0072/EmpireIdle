using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Domain.Services;
using FluentValidation;

namespace EmpireIdle.Application.Marches.Validators
{
    /// <summary>Прискорення маршу — лише на один із варіантів конфіга: довільний відсоток клієнт не замовить.</summary>
    public sealed class SpeedUpMarchCommandValidator : AbstractValidator<SpeedUpMarchCommand>
    {
        public SpeedUpMarchCommandValidator(GameCatalog catalog)
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.MarchId).NotEmpty();
            RuleFor(x => x.Percent)
                .Must(percent => catalog.Config.Monetization.MarchSpeedUps.Any(option => option.Percent == percent))
                .WithMessage("Unknown march speed-up option.");
        }
    }
}
