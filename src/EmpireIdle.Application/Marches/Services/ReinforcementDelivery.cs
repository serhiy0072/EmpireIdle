using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Доставка підкріплень у село союзника.
    ///
    /// Найпростіший зі сценаріїв прибуття: бою немає, армія лишається
    /// в чужому гарнізоні. Але всі три умови — клан, посольство, саме
    /// існування села — перевіряються заново, бо дорога довга.
    /// </summary>
    public sealed class ReinforcementDelivery
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IClanRepository _clanRepository;
        private readonly GameCatalog _catalog;
        private readonly MarchLogistics _logistics;
        private readonly ILogger<ReinforcementDelivery> _logger;

        public ReinforcementDelivery(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IClanRepository clanRepository,
            GameCatalog catalog,
            MarchLogistics logistics,
            ILogger<ReinforcementDelivery> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _clanRepository = clanRepository;
            _catalog = catalog;
            _logistics = logistics;
            _logger = logger;
        }

        /// <summary>
        /// Ставить армію в гарнізон союзника.
        ///
        /// Якщо за час дороги союзник вийшов із клану, село зникло або
        /// посольство переповнилось — армія розвертається. Кидати виняток
        /// тут не можна: це прогін сканера, а не запит гравця, і падіння
        /// заблокувало б решту маршів у пакеті.
        /// </summary>
        public async Task DeliverAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            var units = march.GetUnits();

            var ownerGarrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var ownerVillage = await _villageRepository.GetByIdAsync(ownerGarrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {ownerGarrison.VillageId} not found for garrison {ownerGarrison.Id}.");

            var targetVillage = await _villageRepository.GetByIdAsync(march.TargetId, cancellationToken);
            var targetGarrison = targetVillage is null
                ? null
                : await _garrisonRepository.GetByVillageIdAsync(targetVillage.Id, cancellationToken);

            if (targetVillage is null || targetGarrison is null)
            {
                _logistics.TurnMarchBack(march, units, utcNow);
                return;
            }

            var ownerClan = await _clanRepository.GetClanIdByMemberAsync(ownerVillage.PlayerId, cancellationToken);
            var targetClan = await _clanRepository.GetClanIdByMemberAsync(targetVillage.PlayerId, cancellationToken);

            if (ownerClan is null || ownerClan != targetClan)
            {
                _logger.LogInformation("March {MarchId} turned back: no longer clanmates", march.Id);

                _logistics.TurnMarchBack(march, units, utcNow);
                return;
            }

            var capacity = targetVillage.ReinforcementCapacity(_catalog.Buildings);
            var incoming = units.Values.Sum();

            if (targetGarrison.ReinforcementCount + incoming > capacity)
            {
                _logger.LogInformation(
                    "March {MarchId} turned back: embassy at village {VillageId} has no room for {Incoming} units",
                    march.Id, targetVillage.Id, incoming);

                _logistics.TurnMarchBack(march, units, utcNow);
                return;
            }

            targetGarrison.AddReinforcements(ownerVillage.PlayerId, ownerGarrison.Id, units, capacity, utcNow);
            march.Delivered(utcNow);

            _logger.LogInformation("March {MarchId} delivered {Incoming} units to village {VillageId}",
                march.Id, incoming, targetVillage.Id);
        }
    }
}
