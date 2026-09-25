using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
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
        private readonly IClanStructureRepository _structureRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly MarchCalculator _calculator;
        private readonly GameCatalog _catalog;
        private readonly HeroProgression _progression;
        private readonly ILogger<ReinforcementReturner> _logger;

        public ReinforcementReturner(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IClanStructureRepository structureRepository,
            IMarchRepository marchRepository,
            IHeroRepository heroRepository,
            MarchCalculator calculator,
            GameCatalog catalog,
            HeroProgression progression,
            ILogger<ReinforcementReturner> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _structureRepository = structureRepository;
            _marchRepository = marchRepository;
            _heroRepository = heroRepository;
            _calculator = calculator;
            _catalog = catalog;
            _progression = progression;
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

            // Гарнізони, де стоїть лише герой без юнітів, стеками не знаходяться
            var heroHosts = await _heroRepository.GetForeignGarrisonIdsAsync(ownerPlayerId, cancellationToken);

            foreach (var id in heroHosts.Where(id => hosts.All(h => h.Id != id)))
            {
                var extraHost = await _garrisonRepository.GetByIdAsync(id, cancellationToken);

                if (extraHost is not null)
                    hosts.Add(extraHost);
            }

            var sent = 0;

            foreach (var host in hosts)
                if (await ReturnOwnerAsync(host, ownerPlayerId, utcNow, cancellationToken))
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

            var hostVillage = await _villageRepository.GetByIdAsync(villageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {villageId} not found for garrison {host.Id}.");

            // Список власників знімаємо наперед: WithdrawReinforcements чистить
            // колекцію. Герої господаря не гості, тому фільтруються за власником села
            var stationed = await _heroRepository.GetByGarrisonAsync(host.Id, cancellationToken);

            var owners = host.ReinforcementOwners()
                .Concat(stationed.Where(h => h.PlayerId != hostVillage.PlayerId).Select(h => h.PlayerId))
                .Distinct()
                .ToList();

            var sent = 0;

            foreach (var ownerId in owners)
                if (await ReturnOwnerAsync(host, ownerId, utcNow, cancellationToken))
                    sent++;

            return sent;
        }

        /// <summary>
        /// Знімає війська й героїв одного власника з одного гарнізону
        /// й веде їх додому. Публічний, бо цим же шляхом розбір бою
        /// розпускає контингенти після програної оборони: правило
        /// повернення одне, і дублювати його в бою не можна.
        /// </summary>
        /// <returns>true, якщо марш додому створено.</returns>
        public async Task<bool> ReturnOwnerAsync(Garrison host, Guid ownerPlayerId, DateTime utcNow, CancellationToken cancellationToken)
        {
            var stacks = host.Reinforcements.Where(r => r.OwnerPlayerId == ownerPlayerId).ToList();

            var stationed = await _heroRepository.GetByGarrisonAsync(host.Id, cancellationToken);
            var heroes = stationed.Where(h => h.PlayerId == ownerPlayerId).ToList();

            if (stacks.Count == 0 && heroes.Count == 0)
                return false;

            // Дім знаємо зі стека; якщо стеків немає, лишився тільки герой —
            // тоді дім шукається через село власника
            var ownerGarrison = stacks.Count > 0
                ? await _garrisonRepository.GetByIdAsync(stacks[0].OwnerGarrisonId, cancellationToken)
                : await HomeGarrisonAsync(ownerPlayerId, cancellationToken);

            var ownerVillage = ownerGarrison is null
                ? null
                : await _villageRepository.GetByIdAsync(ownerGarrison.VillageId, cancellationToken);

            var from = await HostLocationAsync(host, cancellationToken);

            if (ownerVillage is null || from is null)
            {
                // Дому більше немає — повертати нікуди; військо просто зникає.
                // Герої лишаються стояти: знищити героя не можна, і дівати
                // його нікуди, поки в гравця немає села
                host.WithdrawReinforcements(ownerPlayerId, utcNow);

                _logger.LogWarning("Reinforcements of {OwnerId} dropped: home village is gone", ownerPlayerId);

                return false;
            }

            var units = host.WithdrawReinforcements(ownerPlayerId, utcNow);

            Guid? escort = null;

            if (heroes.Count > 0)
            {
                heroes[0].SendHome(utcNow);
                escort = heroes[0].Id;
            }

            // Колона йде за найповільнішим, і герой у цьому рахунку нарівні
            // з юнітами: важкий супровід гальмує відхід так само, як облога
            var duration = _calculator.CalculateDuration(
                host.ServerId, from.Value.X, from.Value.Y, ownerVillage.X, ownerVillage.Y, units,
                heroes.Count > 0
                    ? _progression.MarchSpeed(_catalog.FindHero(heroes[0].HeroKey))
                    : null);

            var march = March.ReturningHome(
                Guid.NewGuid(), host.ServerId, ownerGarrison!.Id, escort,
                ownerVillage.X, ownerVillage.Y, from.Value.X, from.Value.Y, from.Value.Id,
                units, duration, utcNow, from.Value.Type);

            await _marchRepository.AddAsync(march, cancellationToken);

            // Решта героїв іде окремо: у марші місце рівно на одного,
            // і кожен рахує власний час — без юнітів його ніщо не тримає
            foreach (var extra in heroes.Skip(1))
            {
                extra.SendHome(utcNow);

                var soloDuration = _calculator.CalculateDuration(
                    host.ServerId, from.Value.X, from.Value.Y, ownerVillage.X, ownerVillage.Y,
                    new Dictionary<UnitStackKey, int>(),
                    _progression.MarchSpeed(_catalog.FindHero(extra.HeroKey)));

                await _marchRepository.AddAsync(March.ReturningHome(
                    Guid.NewGuid(), host.ServerId, ownerGarrison.Id, extra.Id,
                    ownerVillage.X, ownerVillage.Y, from.Value.X, from.Value.Y, from.Value.Id,
                    new Dictionary<UnitStackKey, int>(), soloDuration, utcNow, from.Value.Type), cancellationToken);
            }

            _logger.LogInformation(
                "Reinforcements of {OwnerId} left {HostType} {HostId}: {Count} units, {Heroes} heroes, {Minutes:F1} min home",
                ownerPlayerId, from.Value.Type, from.Value.Id, units.Values.Sum(), heroes.Count, duration.TotalMinutes);

            return true;
        }

        /// <summary>
        /// Розпускає весь гарнізон кланової споруди — коли її зносять або руйнують.
        /// Господаря в споруди немає, тож додому йдуть усі, хто там стоїть.
        /// </summary>
        /// <returns>Скільки маршів вирушило додому.</returns>
        public async Task<int> ReturnAllFromStructureAsync(Garrison host, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var stationed = await _heroRepository.GetByGarrisonAsync(host.Id, cancellationToken);

            var owners = host.ReinforcementOwners()
                .Concat(stationed.Select(h => h.PlayerId))
                .Distinct()
                .ToList();

            var sent = 0;

            foreach (var ownerId in owners)
                if (await ReturnOwnerAsync(host, ownerId, utcNow, cancellationToken))
                    sent++;

            return sent;
        }

        /// <summary>Де стоїть гарнізон-господар: село чи кланова споруда. null — господаря вже немає.</summary>
        private async Task<(Guid Id, int X, int Y, MarchTargetType Type)?> HostLocationAsync(Garrison host,
            CancellationToken cancellationToken)
        {
            if (host.HostKind == GarrisonHost.ClanStructure)
            {
                var structure = await _structureRepository.GetByIdAsync(host.HostId, cancellationToken);

                return structure is null ? null : (structure.Id, structure.X, structure.Y, MarchTargetType.ClanStructure);
            }

            var village = await _villageRepository.GetByIdAsync(host.VillageId, cancellationToken);

            return village is null ? null : (village.Id, village.X, village.Y, MarchTargetType.Village);
        }

        /// <summary>
        /// Гарнізон власника через його село. Потрібен лише тоді, коли в
        /// чужому гарнізоні лишився самий герой без жодного юніта.
        /// </summary>
        private async Task<Garrison?> HomeGarrisonAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellationToken);

            return village is null
                ? null
                : await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken);
        }
    }
}
