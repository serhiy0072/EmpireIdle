using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>
    /// Відправити армію до цілі на карті.
    /// </summary>
    public record SendMarchCommand(
        Guid PlayerId,
        MarchTargetType TargetType,
        Guid TargetId,
        Dictionary<string, int> Units) : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник SendMarchCommand: знімає юнітів із гарнізону,
    /// рахує час дороги й ставить похід у дорогу.
    /// </summary>
    public class SendMarchCommandHandler : IRequestHandler<SendMarchCommand, Guid>
    {
        private const int ServerId = 1; // мультисервер — post-MVP
        private const int MaxActiveMarches = 3;

        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly MarchCalculator _calculator;
        private readonly ILogger<SendMarchCommandHandler> _logger;

        public SendMarchCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IMonsterRepository monsterRepository,
            IUnitOfWork unitOfWork,
            MarchCalculator calculator,
            ILogger<SendMarchCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _monsterRepository = monsterRepository;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _logger = logger;
        }

        public async Task<Guid> Handle(SendMarchCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Ліміт одночасних походів
            var active = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);
            if (active.Count >= MaxActiveMarches)
                throw new InvalidOperationException($"Cannot send more than {MaxActiveMarches} marches at once.");

            var (targetX, targetY) = await ResolveTargetAsync(request, cancellationToken);

            // Знімаємо юнітів із гарнізону (перевірки наявності — всередині)
            garrison.SendUnits(request.Units);

            var duration = _calculator.CalculateDuration(
                ServerId, village.X, village.Y, targetX, targetY, request.Units);

            var march = new March(
                Guid.NewGuid(), ServerId, garrison.Id,
                village.X, village.Y, targetX, targetY,
                request.TargetType, request.TargetId,
                request.Units, DateTime.UtcNow + duration);

            await _marchRepository.AddAsync(march, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "March {MarchId} sent from ({OriginX},{OriginY}) to ({TargetX},{TargetY}), arrives in {Minutes:F1} min",
                march.Id, village.X, village.Y, targetX, targetY, duration.TotalMinutes);

            return march.Id;
        }

        /// <summary>Знаходить координати цілі за її типом.</summary>
        private async Task<(int X, int Y)> ResolveTargetAsync(SendMarchCommand request, CancellationToken cancellationToken)
        {
            switch (request.TargetType)
            {
                case MarchTargetType.Monster:
                    var monster = await _monsterRepository.GetByIdAsync(request.TargetId, cancellationToken)
                        ?? throw new InvalidOperationException($"Monster {request.TargetId} not found.");
                    return (monster.X, monster.Y);

                case MarchTargetType.Village:
                    var target = await _villageRepository.GetByIdAsync(request.TargetId, cancellationToken)
                        ?? throw new InvalidOperationException($"Village {request.TargetId} not found.");
                    return (target.X, target.Y);

                default:
                    throw new InvalidOperationException($"Unsupported target type '{request.TargetType}'.");
            }
        }
    }
}