using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Domain.Services.Config;
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
        private readonly ClanBonuses _clanBonuses;

        public TerritoryBonus(IClanRepository clanRepository, IClanStructureRepository structureRepository,
            ClanTerritoryRules rules, ClanBonuses clanBonuses)
        {
            _clanBonuses = clanBonuses;
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _rules = rules;
        }

        /// <summary>Множник атаки для маршу з цього села; поза територією — 1.</summary>
        public async Task<double> AttackMultiplierAsync(Village village, DateTime utcNow, CancellationToken cancellationToken)
            => await IsCoveredAsync(village, utcNow, cancellationToken)
                ? _rules.AttackMultiplier(true, (await _clanBonuses.ForMemberAsync(village.PlayerId, cancellationToken)).Of(TechnologyStat.TerritoryAttack))
                : 1;

        /// <summary>Множник оборони села разом із підкріпленнями; поза територією — 1.</summary>
        public async Task<double> DefenceMultiplierAsync(Village village, DateTime utcNow, CancellationToken cancellationToken)
            => await DefenceMultiplierAtAsync(village.PlayerId, village.X, village.Y, utcNow, cancellationToken);

        /// <summary>
        /// Множник оборони табору гравця <paramref name="ownerPlayerId"/> на (x, y): територія
        /// рахується за клітинкою табору, а не за селом власника (§2.5).
        /// </summary>
        public async Task<double> DefenceMultiplierAtAsync(Guid ownerPlayerId, int x, int y, DateTime utcNow,
            CancellationToken cancellationToken)
            => await IsCoveredAsync(ownerPlayerId, x, y, utcNow, cancellationToken)
                ? _rules.DefenceMultiplier(true, (await _clanBonuses.ForMemberAsync(ownerPlayerId, cancellationToken)).Of(TechnologyStat.TerritoryDefense))
                : 1;

        /// <summary>
        /// Множник оборони самої споруди: вона завжди в межах власного радіуса, тож добудована — під своїм
        /// бонусом і технологіями свого клану, хоч би хто стояв у її гарнізоні.
        /// </summary>
        public async Task<double> StructureDefenceMultiplierAsync(ClanStructure structure, DateTime utcNow, CancellationToken cancellationToken)
            => _rules.Enabled && structure.IsActiveAt(utcNow)
                ? _rules.DefenceMultiplier(true, (await _clanBonuses.ForClanAsync(structure.ClanId, cancellationToken)).Of(TechnologyStat.TerritoryDefense))
                : 1;

        private Task<bool> IsCoveredAsync(Village village, DateTime utcNow, CancellationToken cancellationToken)
            => IsCoveredAsync(village.PlayerId, village.X, village.Y, utcNow, cancellationToken);

        /// <summary>Чи клітина на території клану гравця — у радіусі діючої споруди (§7.2).</summary>
        public Task<bool> CoversAsync(Guid playerId, int x, int y, DateTime utcNow, CancellationToken cancellationToken)
            => IsCoveredAsync(playerId, x, y, utcNow, cancellationToken);

        private async Task<bool> IsCoveredAsync(Guid playerId, int x, int y, DateTime utcNow, CancellationToken cancellationToken)
        {
            // Світ без території не ходить у базу на кожен бій
            if (!_rules.Enabled)
                return false;

            if (await _clanRepository.GetClanIdByMemberAsync(playerId, cancellationToken) is not Guid clanId)
                return false;

            var structures = await _structureRepository.GetByClanAsync(clanId, cancellationToken);

            return _rules.IsCovered(structures, x, y, utcNow);
        }
    }
}
