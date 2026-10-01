using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Services
{
    /// <summary>
    /// Розпускає клан разом з усім, що тримається за нього лише значенням ClanId, без
    /// зовнішнього ключа: споруди (з клітинами й гарнізонами), заявки, запити допомоги,
    /// прогрес квестів. Каскад БД прибирає лише учасників і ролі.
    /// Масові видалення йдуть одразу в БД, тож викликач тримає транзакцію навколо
    /// цього виклику й свого SaveChanges.
    /// </summary>
    public sealed class ClanDisbander
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanRequestRepository _requestRepository;
        private readonly IClanHelpRepository _helpRepository;
        private readonly IClanQuestRepository _questRepository;
        private readonly ClanStructureRemover _structureRemover;
        private readonly ILogger<ClanDisbander> _logger;

        public ClanDisbander(
            IClanRepository clanRepository,
            IClanStructureRepository structureRepository,
            IClanRequestRepository requestRepository,
            IClanHelpRepository helpRepository,
            IClanQuestRepository questRepository,
            ClanStructureRemover structureRemover,
            ILogger<ClanDisbander> logger)
        {
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _requestRepository = requestRepository;
            _helpRepository = helpRepository;
            _questRepository = questRepository;
            _structureRemover = structureRemover;
            _logger = logger;
        }

        public async Task DisbandAsync(Clan clan, DateTime utcNow, CancellationToken cancellationToken)
        {
            // Спершу споруди: їхні гарнізони йдуть додому маршами, поки споруда ще стоїть
            foreach (var structure in await _structureRepository.GetByClanAsync(clan.Id, cancellationToken))
                await _structureRemover.RemoveAsync(structure, utcNow, cancellationToken);

            var requests = await _requestRepository.RemoveByClanAsync(clan.Id, cancellationToken);
            var helps = await _helpRepository.RemoveByClanAsync(clan.Id, cancellationToken);
            var quests = await _questRepository.RemoveByClanAsync(clan.Id, cancellationToken);

            _clanRepository.Remove(clan);

            _logger.LogInformation(
                "Clan {ClanId} disbanded: removed {Requests} requests, {Helps} help requests, {Quests} quest progress rows",
                clan.Id, requests, helps, quests);
        }
    }
}
