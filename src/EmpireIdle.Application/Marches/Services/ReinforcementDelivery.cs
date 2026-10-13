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
        private readonly IHeroRepository _heroRepository;
        private readonly MarchLogistics _logistics;
        private readonly ReinforcementRules _rules;
        private readonly VillageCapacities _capacities;
        private readonly ILogger<ReinforcementDelivery> _logger;

        public ReinforcementDelivery(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IHeroRepository heroRepository,
            MarchLogistics logistics,
            ReinforcementRules rules,
            VillageCapacities capacities,
            ILogger<ReinforcementDelivery> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _heroRepository = heroRepository;
            _logistics = logistics;
            _rules = rules;
            _capacities = capacities;
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

            // Село переїхало, поки колона йшла (§2.5): підкріплення його не доганяє, а вертається маршем
            if (targetVillage is null || targetGarrison is null
                || targetVillage.X != march.TargetX || targetVillage.Y != march.TargetY)
            {
                await _logistics.TurnMarchBackAsync(march, units, utcNow, cancellationToken);
                return;
            }

            var incoming = units.Values.Sum();

            var refusal = await _rules.CheckOnArrivalAsync(ownerVillage, targetVillage, cancellationToken);

            if (refusal is not null)
            {
                // Клан розпався — назад їде вся колона разом із героєм
                _logger.LogInformation("March {MarchId} turned back: {Reason}", march.Id, refusal);

                await _logistics.TurnMarchBackAsync(march, units, utcNow, cancellationToken);
                return;
            }

            var capacity = _capacities.ReinforcementSlots(targetVillage);
            var free = Math.Max(0, capacity - targetGarrison.ReinforcementCount);

            var (accepted, rejected) = ReinforcementSplit.Take(units, free);

            if (accepted.Count > 0)
                targetGarrison.AddReinforcements(ownerVillage.PlayerId, ownerGarrison.Id, accepted, capacity, utcNow);

            // Герої лишаються завжди, навіть коли не влізло нічого: вони не
            // займають слота посольства, і саме вони тримають бонус над своїми конвоями
            var leaderTaken = false;

            // Найсильніший — першим: вільний слот лідера дістається йому
            foreach (var hero in await _logistics.HeroesOnMarchAsync(march, cancellationToken))
            {
                // Лідерство рахується в межах власника: у господаря свій
                // лідер, у кожного союзника свій над своїм стеком
                var leaderFree = !leaderTaken
                    && await _heroRepository.GetLeaderAsync(targetGarrison.Id, hero.PlayerId, cancellationToken) is null;

                hero.Arrive(targetGarrison.Id, leaderSlotFree: leaderFree, utcNow);
                leaderTaken |= leaderFree;
            }

            if (rejected.Count > 0)
            {
                // Прийняте списується з колони як втрати: механіка та сама,
                // юніти покидають марш. Герої вже зійшли (Arrive знімає
                // з них марш), тож додому залишок їде без них
                march.ApplyLosses(accepted, utcNow);

                await _logistics.TurnMarchBackAsync(march, rejected, utcNow, cancellationToken);

                _logger.LogInformation(
                    "March {MarchId} partially delivered {Accepted} of {Incoming} units to village {VillageId}",
                    march.Id, accepted.Values.Sum(), incoming, targetVillage.Id);

                return;
            }

            march.Delivered(utcNow);

            _logger.LogInformation("March {MarchId} delivered {Incoming} units to village {VillageId}",
                march.Id, incoming, targetVillage.Id);

        }
    }
}
