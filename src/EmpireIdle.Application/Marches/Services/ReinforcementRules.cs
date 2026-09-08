using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using System.Net.NetworkInformation;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Умови, за яких підкріплення можна відправити.
    ///
    /// Живуть окремо, бо перевіряються двічі: при відправленні — щоб армія
    /// не їхала даремно, і на прибутті — бо за час дороги і клан, і
    /// посольство можуть змінитись. Дві копії цих правил розійшлися б.
    /// </summary>
    public sealed class ReinforcementRules
    {
        private readonly IClanRepository _clanRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly GameCatalog _catalog;
        private readonly VillageStatus _status;
        private readonly VillageCapacities _capacities;

        public ReinforcementRules(
            IClanRepository clanRepository,
            IGarrisonRepository garrisonRepository,
            GameCatalog catalog,
            VillageStatus status,
            VillageCapacities capacities)
        {
            _clanRepository = clanRepository;
            _garrisonRepository = garrisonRepository;
            _catalog = catalog;
            _status = status;
            _capacities = capacities;
        }

        /// <summary>
        /// Перевіряє всі умови й кидає на першій невиконаній.
        /// Використовується при відправленні: гравець має побачити причину.
        /// </summary>
        public async Task EnsureAllowedAsync(Village origin, MarchTarget target, int incomingUnits,
            CancellationToken cancellationToken)
        {
            if (target.Village is not { } destination)
                throw new RequirementNotMetException("Reinforcements can only be sent to a village.");

            if (destination.PlayerId == origin.PlayerId)
                throw new RequirementNotMetException("You cannot reinforce your own village.");

            // Підкріплення відкриваються з того самого порогу, що знімає щит:
            // недоторканне село інакше стало б сейфом для кланової армії
            var shieldLevel = _catalog.Config.Combat.NewbieShieldTownHallLevel;

            if (_status.IsShielded(origin))
                throw new RequirementNotMetException(
                    $"Reinforcements are available from town hall level {shieldLevel}.");

            if (_status.IsShielded(destination))
                throw new RequirementNotMetException("This village cannot receive reinforcements yet.");

            if (!await AreClanmatesAsync(origin.PlayerId, destination.PlayerId, cancellationToken))
                throw new RequirementNotMetException("Reinforcements go to clanmates only.");

            var free = await FreeEmbassySlotsAsync(destination, cancellationToken);

            if (incomingUnits > free)
                throw new RequirementNotMetException(
                    $"The embassy has room for {free} more units, you are sending {incomingUnits}.");
        }

        /// <summary>
        /// Те саме, але без винятків: на прибутті відмова означає розворот,
        /// а не помилку. Це прогін сканера, і падіння заблокувало б
        /// решту маршів у пакеті.
        /// </summary>
        /// <returns>Причина відмови або null, якщо доставку дозволено.</returns>
        public async Task<string?> CheckOnArrivalAsync(Village origin, Village destination, int incomingUnits,
            CancellationToken cancellationToken)
        {
            if (!await AreClanmatesAsync(origin.PlayerId, destination.PlayerId, cancellationToken))
                return "no longer clanmates";

            var free = await FreeEmbassySlotsAsync(destination, cancellationToken);

            return incomingUnits > free
                ? $"embassy has room for {free} of {incomingUnits} units"
                : null;
        }

        /// <summary>Скільки чужих юнітів село ще прийме.</summary>
        public async Task<int> FreeEmbassySlotsAsync(Village destination, CancellationToken cancellationToken)
        {
            var garrison = await _garrisonRepository.GetByVillageIdAsync(destination.Id, cancellationToken);

            if (garrison is null)
                return 0;

            return _capacities.ReinforcementSlots(destination) - garrison.ReinforcementCount;
        }

        private async Task<bool> AreClanmatesAsync(Guid first, Guid second, CancellationToken cancellationToken)
        {
            var firstClan = await _clanRepository.GetClanIdByMemberAsync(first, cancellationToken);

            if (firstClan is null)
                return false;

            var secondClan = await _clanRepository.GetClanIdByMemberAsync(second, cancellationToken);

            return firstClan == secondClan;
        }
    }
}
