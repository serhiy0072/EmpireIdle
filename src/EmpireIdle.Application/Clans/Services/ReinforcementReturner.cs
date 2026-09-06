using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Services
{
    /// <summary>
    /// Відправляє підкріплення додому. Один сервіс на три приводи —
    /// відкликання, вихід із клану і кік: усі три роблять те саме,
    /// а розкидані по хендлерах розійшлися б на першій же правці.
    ///
    /// Юніти не телепортуються: зняті з гарнізону, вони йдуть маршем
    /// і доступні власнику лише після прибуття.
    /// </summary>
    public sealed class ReinforcementReturner
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly MarchCalculator _calculator;
        private readonly ILogger<ReinforcementReturner> _logger;

        public ReinforcementReturner(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IMarchRepository marchRepository,
            MarchCalculator calculator,
            ILogger<ReinforcementReturner> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _marchRepository = marchRepository;
            _calculator = calculator;
            _logger = logger;
        }

        /// <summary>
        /// Забирає війська гравця з усіх чужих сіл. Викликається при виході
        /// з клану, кіку й повному відкликанні.
        /// </summary>
        /// <returns>Скільки маршів вирушило додому.</returns>
        public async Task<int> ReturnAllOfPlayerAsync(Guid ownerPlayerId, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var hosts = await _garrisonRepository.GetHoldingReinforcementsAsync(ownerPlayerId, cancellationToken);

            var sent = 0;

            foreach (var host in hosts)
                if (await ReturnFromHostAsync(host, ownerPlayerId, utcNow, cancellationToken))
                    sent++;

            return sent;
        }

        /// <summary>
        /// Розпускає всі чужі війська з села гравця — коли з клану виходить
        /// сам господар. Гості не мають лишатись у селі поза кланом.
        /// </summary>
        /// <returns>Скільки маршів вирушило додому.</returns>
        public async Task<int> ReturnAllFromVillageAsync(Guid villageId, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var host = await _garrisonRepository.GetByVillageIdAsync(villageId, cancellationToken);

            if (host is null)
                return 0;

            var sent = 0;

            // Список власників знімаємо наперед: WithdrawReinforcements чистить колекцію
            foreach (var ownerId in host.ReinforcementOwners())
                if (await ReturnFromHostAsync(host, ownerId, utcNow, cancellationToken))
                    sent++;

            return sent;
        }

        /// <summary>Знімає війська одного власника з одного гарнізону й веде їх додому.</summary>
        private async Task<bool> ReturnFromHostAsync(Garrison host, Guid ownerPlayerId, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var stacks = host.Reinforcements.Where(r => r.OwnerPlayerId == ownerPlayerId).ToList();

            if (stacks.Count == 0)
                return false;

            var ownerGarrisonId = stacks[0].OwnerGarrisonId;

            var ownerGarrison = await _garrisonRepository.GetByIdAsync(ownerGarrisonId, cancellationToken);
            var ownerVillage = ownerGarrison is null
                ? null
                : await _villageRepository.GetByIdAsync(ownerGarrison.VillageId, cancellationToken);

            var hostVillage = await _villageRepository.GetByIdAsync(host.VillageId, cancellationToken);

            if (ownerVillage is null || hostVillage is null)
            {
                // Дому більше немає — повертати нікуди; військо просто зникає
                host.WithdrawReinforcements(ownerPlayerId, utcNow);

                _logger.LogWarning("Reinforcements of {OwnerId} dropped: home village is gone", ownerPlayerId);

                return false;
            }

            var units = host.WithdrawReinforcements(ownerPlayerId, utcNow);

            if (units.Count == 0)
                return false;

            var duration = _calculator.CalculateDuration(
                host.ServerId, hostVillage.X, hostVillage.Y, ownerVillage.X, ownerVillage.Y, units);

            var march = March.ReturningHome(
                Guid.NewGuid(), host.ServerId, ownerGarrisonId,
                ownerVillage.X, ownerVillage.Y, hostVillage.X, hostVillage.Y, hostVillage.Id,
                units, duration, utcNow);

            await _marchRepository.AddAsync(march, cancellationToken);

            _logger.LogInformation(
                "Reinforcements of {OwnerId} left village {VillageId}: {Count} units, {Minutes:F1} min home",
                ownerPlayerId, hostVillage.Id, units.Values.Sum(), duration.TotalMinutes);

            return true;
        }
    }
}
