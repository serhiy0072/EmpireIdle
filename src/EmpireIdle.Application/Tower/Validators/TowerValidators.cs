using EmpireIdle.Application.Tower.Commands;
using EmpireIdle.Application.Tower.Queries;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Services;
using FluentValidation;

namespace EmpireIdle.Application.Tower.Validators
{
    public sealed class StormTowerFloorCommandValidator : AbstractValidator<StormTowerFloorCommand>
    {
        public StormTowerFloorCommandValidator(GameCatalog catalog)
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Floor).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Lanes).Squad(catalog);
            RuleFor(x => x.Casts!).Casts(catalog).When(x => x.Casts is not null);
        }
    }

    public sealed class PreviewTowerFloorQueryValidator : AbstractValidator<PreviewTowerFloorQuery>
    {
        public PreviewTowerFloorQueryValidator(GameCatalog catalog)
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Floor).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Lanes).Squad(catalog);
            RuleFor(x => x.Casts).NotNull().Casts(catalog);
        }
    }

    /// <summary>Правила загону й наказів, спільні для штурму й перегляду ручного бою.</summary>
    internal static class TowerRules
    {
        /// <summary>Бій на 90 с при перезарядці від секунди дає десятки наказів; більше — лише сміття.</summary>
        public const int MaxCasts = 200;

        // Розмір загону — з конфіга, той самий, що в рушії: інакше відмова й текст розходились би
        public static IRuleBuilderOptions<T, IReadOnlyList<Guid?>> Squad<T>(this IRuleBuilder<T, IReadOnlyList<Guid?>> rule, GameCatalog catalog)
        {
            var size = catalog.Config.RealtimeBattle.SquadSize;

            return rule
                .NotEmpty()
                .Must(lanes => lanes.Count <= size && lanes.Any(id => id is not null))
                .Must(lanes => lanes.OfType<Guid>().Distinct().Count() == lanes.OfType<Guid>().Count())
                .WithMessage($"A squad needs 1–{size} distinct heroes on at most {size} lanes.");
        }

        /// <summary>Наказ — у межах бою й на доріжці загону; їх не більше за MaxCasts.</summary>
        public static IRuleBuilderOptions<T, IReadOnlyList<ManualCast>> Casts<T>(this IRuleBuilder<T, IReadOnlyList<ManualCast>> rule,
            GameCatalog catalog)
        {
            var battle = catalog.Config.RealtimeBattle;
            var maxTick = battle.MaxBattleSeconds * 1000 / battle.TickMilliseconds;

            return rule
                .Must(casts => casts.Count <= MaxCasts)
                .Must(casts => casts.All(c => c.Tick >= 1 && c.Tick <= maxTick && c.Lane >= 0 && c.Lane < battle.SquadSize))
                .WithMessage($"Up to {MaxCasts} skill orders, each within ticks 1–{maxTick} and lanes 0–{battle.SquadSize - 1}.");
        }
    }
}
