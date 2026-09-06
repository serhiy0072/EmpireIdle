using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Допомогти союзнику з таймером.
    ///
    /// Кожна допомога ріже фіксовану частку повного часу, а кап зупиняє
    /// на сорока відсотках. Клан робить донат дешевшим, не непотрібним:
    /// якби він міг обнулити таймер, продаж прискорень помер би того ж дня.
    /// </summary>
    public record GiveClanHelpCommand(Guid PlayerId, Guid RequestId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class GiveClanHelpCommandHandler : IRequestHandler<GiveClanHelpCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanHelpRepository _helpRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<GiveClanHelpCommandHandler> _logger;

        public GiveClanHelpCommandHandler(
            IClanRepository clanRepository,
            IClanHelpRepository helpRepository,
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<GiveClanHelpCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _helpRepository = helpRepository;
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(GiveClanHelpCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException("You are not in a clan.");

            var helpRequest = await _helpRepository.GetByIdAsync(request.RequestId, cancellationToken)
                ?? throw new EntityNotFoundException("Help request", request.RequestId);

            // Допомагати можна лише своїм: запит іншого клану не має бути видимий,
            // але перевірка тут — на випадок підібраного id
            if (helpRequest.ClanId != clan.Id)
                throw new RequirementNotMetException("That request belongs to another clan.");

            var clanConfig = _catalog.Config.Clan;
            var maxHelpers = clanConfig.MaxHelpers;

            // Агрегат перевіряє повтор, кап і строк — і повертає, скільки зрізати
            var reduction = helpRequest.AcceptHelp(
                request.PlayerId, clanConfig.HelpSharePerPlayer, maxHelpers, now);

            await ApplyReductionAsync(helpRequest, reduction, now, cancellationToken);

            clan.RecordActivity(request.PlayerId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Player {HelperId} helped {OwnerId} with {TargetType} {TargetId}: −{Minutes:F1} min ({Count}/{Max})",
                request.PlayerId, helpRequest.PlayerId, helpRequest.TargetType, helpRequest.TargetId,
                reduction.TotalMinutes, helpRequest.HelpCount, maxHelpers);
        }

        /// <summary>
        /// Зрізає час на самому таймері. Іде тим самим шляхом, що прискорення
        /// за gems — інакше два способи скоротити таймер розійшлися б у поведінці.
        /// </summary>
        private async Task ApplyReductionAsync(Domain.Entities.ClanHelpRequest helpRequest, TimeSpan reduction,
            DateTime now, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(helpRequest.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", helpRequest.PlayerId);

            switch (helpRequest.TargetType)
            {
                case ClanHelpTarget.Construction:
                    var building = village.Buildings.FirstOrDefault(b => b.Id == helpRequest.TargetId)
                        ?? throw new EntityNotFoundException("Building", helpRequest.TargetId);

                    if (!building.IsUnderConstruction)
                        throw new InvalidStateException("That building is no longer under construction.");

                    building.ReduceConstructionTime(reduction);
                    break;

                case ClanHelpTarget.Training:
                    var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                        ?? throw new EntityNotFoundException("Garrison for village", village.Id);

                    garrison.ReduceTrainingTime(helpRequest.TargetId, reduction, now);
                    break;

                default:
                    throw new RequirementNotMetException($"Unsupported help target '{helpRequest.TargetType}'.");
            }
        }
    }
}
