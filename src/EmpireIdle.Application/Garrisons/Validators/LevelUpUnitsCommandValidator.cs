using EmpireIdle.Application.Garrisons.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Garrisons.Validators
{
    /// <summary>
    /// Межі рівнів відсікаються до розрахунку ціни: вона сумує крок за кроком
    /// від FromLevel, тож від'ємний FromLevel дав би мільярди ітерацій на запит.
    /// Стеля ToLevel — в обробнику, бо залежить від конфіга й ратуші.
    /// </summary>
    public sealed class LevelUpUnitsCommandValidator : AbstractValidator<LevelUpUnitsCommand>
    {
        public LevelUpUnitsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.UnitType).NotEmpty().MaximumLength(50);
            RuleFor(x => x.FromLevel).GreaterThanOrEqualTo(1);
            RuleFor(x => x.ToLevel).GreaterThan(x => x.FromLevel);
            RuleFor(x => x.Count).GreaterThan(0);
        }
    }
}
