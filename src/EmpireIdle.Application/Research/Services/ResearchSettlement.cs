using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Research.Services
{
    /// <summary>
    /// Готує завершення дослідження: технологія виробітку змінює ставку будівель, тож вироблене
    /// до цього моменту фіксується за старою ставкою — інакше новий рівень заднім числом
    /// перерахував би весь накопичений буфер. Так само село матеріалізується перед зміною кільця карти.
    /// Матеріалізація нічого не змінює в сумі, тому її можна робити й тоді, коли дослідження ще не дозріє.
    /// </summary>
    public sealed class ResearchSettlement
    {
        private readonly IVillageRepository _villages;
        private readonly IServerRepository _servers;
        private readonly EffectResolver _effects;
        private readonly WorldGeometry _geometry;
        private readonly GameCatalog _catalog;
        private readonly PersonalTechnologies _technologies;

        public ResearchSettlement(IVillageRepository villages, IServerRepository servers, EffectResolver effects,
            WorldGeometry geometry, GameCatalog catalog, PersonalTechnologies technologies)
        {
            _villages = villages;
            _servers = servers;
            _effects = effects;
            _geometry = geometry;
            _catalog = catalog;
            _technologies = technologies;
        }

        /// <summary>Фіксує виробіток села на цей момент, якщо активне дослідження змінює виробіток.</summary>
        public async Task SettleAsync(PlayerResearch research, DateTime utcNow, CancellationToken cancellationToken)
        {
            if (research.ActiveKey is not { } active
                || TechnologyKey.Parse(active) is not { } key
                || _technologies.Tree.Node(key.Node) is not { } node
                || node.Effects.All(e => e.Stat != TechnologyStat.Production))
                return;

            var village = await _villages.GetByPlayerIdAsync(research.PlayerId, cancellationToken);

            if (village is null)
                return;

            var serverLevel = await _servers.GetLevelAsync(village.ServerId, cancellationToken);
            var boost = await _effects.GetProductionBoostAsync(research.PlayerId, utcNow, cancellationToken);

            village.MaterializeProduction(_catalog.Buildings, utcNow, boost,
                _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel));
        }
    }
}
