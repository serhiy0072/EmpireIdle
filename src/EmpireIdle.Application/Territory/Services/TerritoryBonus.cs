using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Territory.Services
{
    /// <summary>
    /// Бонус кланової території для конкретного села (GDD §7.2): сталий, якщо село
    /// в радіусі хоч однієї добудованої споруди свого клану. Діє на все, що б'ється
    /// звідти, — оборону з підкріпленнями й атаки, що вирушають із села.
    /// </summary>
    public sealed class TerritoryBonus
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly ClanTerritoryRules _rules;

        public TerritoryBonus(IClanRepository clanRepository, IClanStructureRepository structureRepository,
            ClanTerritoryRules rules)
        {
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _rules = rules;
        }

        /// <summary>Множник атаки для маршу з цього села; поза територією — 1.</summary>
        public async Task<double> AttackMultiplierAsync(Village village, DateTime utcNow, CancellationToken cancellationToken)
            => _rules.AttackMultiplier(await IsCoveredAsync(village, utcNow, cancellationToken));

        /// <summary>Множник оборони села разом із підкріпленнями; поза територією — 1.</summary>
        public async Task<double> DefenceMultiplierAsync(Village village, DateTime utcNow, CancellationToken cancellationToken)
            => _rules.DefenceMultiplier(await IsCoveredAsync(village, utcNow, cancellationToken));

        private async Task<bool> IsCoveredAsync(Village village, DateTime utcNow, CancellationToken cancellationToken)
        {
            // Світ без території не ходить у базу на кожен бій
            if (!_rules.Enabled)
                return false;

            if (await _clanRepository.GetClanIdByMemberAsync(village.PlayerId, cancellationToken) is not Guid clanId)
                return false;

            var structures = await _structureRepository.GetByClanAsync(clanId, cancellationToken);

            return _rules.IsCovered(structures, village.X, village.Y, utcNow);
        }
    }
}
