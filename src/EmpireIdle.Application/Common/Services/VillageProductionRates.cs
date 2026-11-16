using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>Множники виробітку села на цей момент: вікна бустів і кільце карти.</summary>
    public sealed record ProductionRates(ProductionBoost Boost, double LocationMultiplier);

    /// <summary>
    /// Збирає множники виробітку села: без них буфер будівлі не порахувати. Потрібні всюди, де
    /// змінюється ставка, — зокрема на завершенні апгрейду: вироблене до нього фіксується за старим рівнем.
    /// </summary>
    public sealed class VillageProductionRates
    {
        private readonly IServerRepository _servers;
        private readonly EffectResolver _effects;
        private readonly WorldGeometry _geometry;

        public VillageProductionRates(IServerRepository servers, EffectResolver effects, WorldGeometry geometry)
        {
            _servers = servers;
            _effects = effects;
            _geometry = geometry;
        }

        public async Task<ProductionRates> ForAsync(Village village, DateTime utcNow, CancellationToken cancellationToken)
        {
            var boost = await _effects.GetProductionBoostAsync(village.PlayerId, utcNow, cancellationToken);
            var serverLevel = await _servers.GetLevelAsync(village.ServerId, cancellationToken);

            return new ProductionRates(boost, _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel));
        }
    }
}
