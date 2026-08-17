using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Повертає діючі множники гравця. Прострочені ефекти ігноруються,
    /// відсутні дають нейтральний множник 1.0.
    /// </summary>
    public class EffectResolver
    {
        private readonly IActiveEffectRepository _repository;

        public EffectResolver(IActiveEffectRepository repository)
        {
            _repository = repository;
        }

        /// <summary>Множник для однієї цілі.</summary>
        public async Task<double> GetMultiplierAsync(Guid playerId, EffectTarget target, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var effect = await _repository.GetAsync(playerId, target, cancellationToken);

            return effect is not null && effect.IsActive(utcNow)
                ? effect.Multiplier
                : 1.0;
        }

        /// <summary>Усі діючі множники гравця одним запитом.</summary>
        public async Task<Dictionary<EffectTarget, double>> GetAllAsync(Guid playerId, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var effects = await _repository.GetByPlayerAsync(playerId, cancellationToken);

            return effects
                .Where(e => e.IsActive(utcNow))
                .ToDictionary(e => e.Target, e => e.Multiplier);
        }

        /// <summary>Вікно буста виробництва — для розрахунку буфера за минулий період.</summary>
        public async Task<ProductionBoost> GetProductionBoostAsync(Guid playerId, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var effect = await _repository.GetAsync(playerId, EffectTarget.Production, cancellationToken);

            // Прострочений буст теж потрібен: він міг діяти частину періоду
            return effect is null
                ? ProductionBoost.None
                : new ProductionBoost(effect.Multiplier, effect.StartedAt, effect.ExpiresAt);
        }
    }
}
