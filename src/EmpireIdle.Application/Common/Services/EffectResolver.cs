using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Повертає діючі множники гравця: бусти крамниці й пасивки звірів (GDD §5.10).
    /// Джерела складаються додаванням — ×2 від буста і +15% від звіра дають ×2.15.
    /// Прострочені ефекти ігноруються, відсутні дають нейтральний множник 1.0.
    /// </summary>
    public class EffectResolver
    {
        private readonly IActiveEffectRepository _repository;
        private readonly IBeastPenRepository _pens;
        private readonly GameCatalog _catalog;
        private readonly BeastProgression _beasts;

        public EffectResolver(IActiveEffectRepository repository, IBeastPenRepository pens, GameCatalog catalog,
            BeastProgression beasts)
        {
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

            return boost + beasts;
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

            return boost;
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
