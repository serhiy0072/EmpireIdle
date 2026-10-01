using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Rating.Commands
{
    /// <summary>Перераховує рейтинг усіх гравців поточного світу.</summary>
    public record RecalculateAllRatingsCommand : IRequest;

    public sealed class RecalculateAllRatingsCommandHandler : IRequestHandler<RecalculateAllRatingsCommand>
    {
        /// <summary>Скільки гравців на одне збереження: конфлікт відкочує лише свою пачку.</summary>
        private const int BatchSize = 200;

        private readonly IPlayerRatingRepository _ratingRepository;
        private readonly IPlayerPowerRepository _powerRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RecalculateAllRatingsCommandHandler> _logger;

        public RecalculateAllRatingsCommandHandler(
            IPlayerRatingRepository ratingRepository,
            IPlayerPowerRepository powerRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<RecalculateAllRatingsCommandHandler> logger)
        {
            _ratingRepository = ratingRepository;
            _powerRepository = powerRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RecalculateAllRatingsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Села й сила — одним читанням на світ: годинний джоб без N+1. Рейтинги — пачками,
            // кожна своїм збереженням: outbox паралельно оновлює рейтинги окремих гравців, і
            // один конфлікт xmin не має відкотити весь світ — пропущена пачка наздожене наступним прогоном
            var villages = await _villageRepository.GetAllWithBuildingsAsync(cancellationToken);
            var powers = await _powerRepository.GetAllAsync(cancellationToken);

            var powerByPlayer = powers.ToDictionary(p => p.PlayerId, p => p.TotalPower);
            var skipped = 0;

            foreach (var batch in villages.Chunk(BatchSize))
            {
                var ratingByPlayer = (await _ratingRepository.GetByPlayersAsync(
                        batch.Select(v => v.PlayerId).ToList(), cancellationToken))
                    .ToDictionary(r => r.PlayerId);

                foreach (var village in batch)
                {
                    if (!ratingByPlayer.TryGetValue(village.PlayerId, out var rating))
                    {
                        rating = new PlayerRating(Guid.NewGuid(), village.PlayerId, village.ServerId, now);
                        await _ratingRepository.AddAsync(rating, cancellationToken);
                    }

                    var power = powerByPlayer.GetValueOrDefault(village.PlayerId);
                    var development = village.Buildings.Sum(b => b.Level.Value);

                    rating.Recalculate(power, development, _catalog.Config.Rating, now);
                }

                if (!await _unitOfWork.TrySaveChangesAsync(cancellationToken))
                    skipped += batch.Length;
            }

            if (skipped > 0)
                _logger.LogWarning("Rating recalculation skipped {Count} players on concurrency conflicts", skipped);

            _logger.LogInformation("Recalculated ratings for {Count} players", villages.Count);
        }
    }
}

