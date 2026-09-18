using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Dev.Commands
{
    /// <summary>
    /// Наливає поточному гравцю все, на чому можна дивитись екрани.
    ///
    /// Видача йде через RewardDispatcher, а не прямим записом у репозиторії:
    /// сід тоді ганяє ті самі шляхи, що й квести з банерами, і не розходиться
    /// з ними при змінах. Ендпоінт мапиться лише в Development.
    /// </summary>
    public record SeedDevAccountCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest;

    internal sealed class SeedDevAccountCommandHandler : IRequestHandler<SeedDevAccountCommand>
    {
        private const int Gems = 5_000;
        private const int ResourceAmount = 100_000;
        private const int StackableAmount = 10;

        /// <summary>Друга видача того самого героя приходить уламками — видно обидва шляхи.</summary>
        private const int HeroCopies = 2;

        private readonly GameCatalog _catalog;
        private readonly RewardDispatcher _dispatcher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SeedDevAccountCommandHandler> _logger;

        public SeedDevAccountCommandHandler(
            GameCatalog catalog,
            RewardDispatcher dispatcher,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<SeedDevAccountCommandHandler> logger)
        {
            _catalog = catalog;
            _dispatcher = dispatcher;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(SeedDevAccountCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var rewards = Build();

            await _dispatcher.GrantAllAsync(request.PlayerId, rewards, "dev-seed", now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Dev seed granted {Count} rewards to player {PlayerId}", rewards.Count, request.PlayerId);
        }

        private List<RewardConfig> Build()
        {
            var config = _catalog.Config;

            var rewards = new List<RewardConfig>
            {
                new() { Type = "Gems", Amount = Gems }
            };

            rewards.AddRange(config.Resources
                .Select(resource => new RewardConfig { Type = "Resource", Key = resource.Key, Amount = ResourceAmount }));

            // Звичайні герої купуються за золото в залі, їх сідити нема сенсу.
            // Беремо по одному представнику кожного вищого рангу
            var heroes = config.Heroes
                .Where(hero => hero.Rank != Rarity.Common)
                .GroupBy(hero => hero.Rank)
                .Select(group => group.First())
                .ToList();

            for (var copy = 0; copy < HeroCopies; copy++)
            {
                rewards.AddRange(heroes.Select(hero => new RewardConfig { Type = "Hero", Key = hero.Key, Amount = 1 }));
            }

            // Слот не null — це спорядження: зброя і артефакти, кожен зі своїм роллом
            rewards.AddRange(config.Items
                .Where(item => item.Slot is not null)
                .Select(item => new RewardConfig { Type = "Equipment", Key = item.Key, Amount = 1 }));

            rewards.AddRange(config.Items
                .Where(item => item.Slot is null)
                .Select(item => new RewardConfig { Type = "Item", Key = item.Key, Amount = StackableAmount }));

            return rewards;
        }
    }
}
