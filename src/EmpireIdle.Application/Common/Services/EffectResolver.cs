using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Повертає діючі множники гравця: бусти крамниці, пасивки звірів (GDD §5.10) і технології
    /// швидкості маршу та вантажу (рішення 28.10.2026). Джерела складаються додаванням —
    /// ×2 від буста, +15% від звіра і +5% від технологій дають ×2.2.
    /// Прострочені ефекти ігноруються, відсутні дають нейтральний множник 1.0.
    /// </summary>
    public class EffectResolver
    {
        private readonly IActiveEffectRepository _repository;
        private readonly IBeastPenRepository _pens;
        private readonly GameCatalog _catalog;
        private readonly BeastProgression _beasts;
        private readonly PlayerTechnologies _technologies;

        public EffectResolver(IActiveEffectRepository repository, IBeastPenRepository pens, GameCatalog catalog,
            BeastProgression beasts, PlayerTechnologies technologies)
        {
            _technologies = technologies;
            _repository = repository;
            _pens = pens;
            _catalog = catalog;
            _beasts = beasts;
        }

        /// <summary>Множник для однієї цілі.</summary>
        public async Task<double> GetMultiplierAsync(Guid playerId, EffectTarget target, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var effect = await _repository.GetAsync(playerId, target, cancellationToken);

            var boost = effect is not null && effect.IsActive(utcNow)
                ? effect.Multiplier
                : 1.0;

            var beasts = (await PassivesAsync(playerId, target, cancellationToken))
                .Where(p => p.Beast.IsActiveAt(utcNow))
                .Sum(p => p.Bonus);

            return boost + beasts + await TechnologyAsync(playerId, target, cancellationToken);
        }

        /// <summary>
        /// Надбавка технологій до цілі. Атаку й захист технології дають через бонус стека (ArmyBuffs),
        /// виробіток — множником ставки в ProductionBoost; тут лише ті, що стають в один пул із бустами й звірами.
        /// </summary>
        private async Task<double> TechnologyAsync(Guid playerId, EffectTarget target, CancellationToken cancellationToken)
        {
            TechnologyStat? stat = target switch
            {
                EffectTarget.MarchSpeed => TechnologyStat.MarchSpeed,
                EffectTarget.Carry => TechnologyStat.Carry,
                _ => null
            };

            return stat is { } known
                ? (await _technologies.ForPlayerAsync(playerId, cancellationToken)).Of(known)
                : 0;
        }

        /// <summary>
        /// Вікна бустів виробництва — для розрахунку буфера за минулий період.
        /// Прострочені теж потрібні: вони могли діяти частину періоду.
        /// </summary>
        public async Task<ProductionBoost> GetProductionBoostAsync(Guid playerId, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var effect = await _repository.GetAsync(playerId, EffectTarget.Production, cancellationToken);

            var boost = effect is null
                ? ProductionBoost.None
                : new ProductionBoost(effect.Multiplier, effect.StartedAt, effect.ExpiresAt);

            foreach (var (beast, bonus) in await PassivesAsync(playerId, EffectTarget.Production, cancellationToken))
                if (beast.ActivatedAt is { } from && beast.ActiveUntil is { } until)
                    boost = boost.With(new BoostWindow(bonus, from, until));

            var technologies = await _technologies.ForPlayerAsync(playerId, cancellationToken);
            var byResource = technologies.Targets(TechnologyStat.Production)
                .Where(t => t.Target is not null)
                .ToDictionary(t => t.Target!, t => t.Value);

            return boost with
            {
                TechnologyForAll = technologies.Of(TechnologyStat.Production),
                TechnologyByResource = byResource
            };
        }

        /// <summary>Звірі гравця з пасивкою на цю ціль і їхня надбавка на нинішньому рівні.</summary>
        private async Task<List<(Beast Beast, double Bonus)>> PassivesAsync(Guid playerId, EffectTarget target,
            CancellationToken cancellationToken)
        {
            var pen = await _pens.GetByPlayerReadOnlyAsync(playerId, cancellationToken);

            if (pen is null)
                return [];

            return pen.Beasts
                .Where(b => b.ActivatedAt is not null
                            && _catalog.Beasts.TryGetValue(b.BeastKey, out var config) && config.Effect == target)
                .Select(b => (b, _beasts.Bonus(_catalog.Beasts[b.BeastKey], b.Level)))
                .ToList();
        }
    }
}
