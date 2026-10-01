using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Кого можна атакувати: власне село й табір — ні, села й табори соклановців — теж ні.
    ///
    /// Одне правило на відправку, прев'ю й прибуття: клан міг змінитися, поки марш ішов,
    /// і розійдися перевірки — напад на соклановця проходив би вже на місці.
    /// </summary>
    public sealed class HostilityRules
    {
        private readonly IClanRepository _clanRepository;

        public HostilityRules(IClanRepository clanRepository) => _clanRepository = clanRepository;

        /// <param name="camp">Ціль — табір, а не село: відмова тоді називає табір.</param>
        /// <returns>null — нападати можна; інакше причина відмови.</returns>
        public async Task<RefusalReason?> RefusalAsync(Guid attackerPlayerId, Guid defenderPlayerId, bool camp,
            CancellationToken cancellationToken)
        {
            if (attackerPlayerId == defenderPlayerId)
                return camp ? RefusalReasons.MarchOwnCamp : RefusalReasons.MarchOwnVillage;

            var attackerClan = await _clanRepository.GetClanIdByMemberAsync(attackerPlayerId, cancellationToken);

            if (attackerClan is null)
                return null;

            var defenderClan = await _clanRepository.GetClanIdByMemberAsync(defenderPlayerId, cancellationToken);

            return attackerClan == defenderClan ? RefusalReasons.MarchClanmate : null;
        }
    }
}
