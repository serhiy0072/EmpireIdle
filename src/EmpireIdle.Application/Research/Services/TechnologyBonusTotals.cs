using EmpireIdle.Application.Research.ReadModels;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Research.Services
{
    /// <summary>Сумарні бонуси дерева для екрана «що вже дають технології»: спільне для Лабораторії й клану.</summary>
    public static class TechnologyBonusTotals
    {
        /// <summary>Сума за статами й цілями, лише ненульові.</summary>
        public static List<TechnologyBonusView> Of(TechnologyBonuses bonuses, GameConfig config)
        {
            var targets = new List<string?> { null };
            targets.AddRange(config.Units.Select(u => u.Key));
            targets.AddRange(config.Resources.Select(r => r.Key));

            return Enum.GetValues<TechnologyStat>()
                .SelectMany(stat => targets.Select(target => (stat, target,
                    value: target is null ? bonuses.Of(stat) : bonuses.Of(stat, target) - bonuses.Of(stat))))
                .Where(x => Math.Abs(x.value) > 1e-9)
                .Select(x => new TechnologyBonusView(x.stat, x.target, x.value))
                .ToList();
        }
    }
}
