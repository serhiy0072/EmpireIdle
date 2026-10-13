using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Heroes.Services
{
    /// <summary>
    /// Спільне для команд табору (GDD §6.1): сам табір гравця, синхронізація табірного рівня
    /// й оплата gems. Окремо від обробників, бо синхронізацію викликають і прокачка, і скидання рівня.
    /// </summary>
    public class TrainingCampService
    {
        private readonly IHeroRepository _heroes;
        private readonly IPlayerRepository _players;
        private readonly IPlayerWalletRepository _wallets;
        private readonly IServerContext _serverContext;
        private readonly TrainingCampRules _rules;

        public TrainingCampService(
            IHeroRepository heroes,
            IPlayerRepository players,
            IPlayerWalletRepository wallets,
            IServerContext serverContext,
            TrainingCampRules rules)
        {
            _heroes = heroes;
            _players = players;
            _wallets = wallets;
            _serverContext = serverContext;
            _rules = rules;
        }

        /// <summary>Табір гравця; першого звернення створюється — окремої дії «збудувати табір» немає.</summary>
        public async Task<TrainingCamp> GetOrCreateCampAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken)
        {
            var camp = await _heroes.GetCampAsync(playerId, cancellationToken);

            if (camp is not null)
                return camp;

            camp = new TrainingCamp(Guid.NewGuid(), playerId, _serverContext.ServerId, utcNow);
            await _heroes.AddCampAsync(camp, cancellationToken);

            return camp;
        }

        /// <summary>
        /// Переносить рівень опорної п'ятірки на героїв у слотах. Викликати після будь-якої зміни,
        /// що могла його зсунути: прокачки, скидання, руху героїв у табір і з нього.
        /// </summary>
        public async Task SyncAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken)
            => Sync(await _heroes.GetByPlayerAsync(playerId, cancellationToken), utcNow);

        /// <inheritdoc cref="SyncAsync"/>
        public void Sync(IReadOnlyCollection<Hero> heroes, DateTime utcNow)
        {
            // Без п'ятірки поза табором лишаємо останній знімок: так буває лише в мить, коли
            // героя ставлять у табір, а цю мить команда розміщення відсікає сама
            if (_rules.CampLevel(heroes) is not { } level)
                return;

            foreach (var hero in heroes.Where(h => h.CampSlot is not null))
                hero.SyncCampLevel(level, utcNow);
        }

        /// <summary>Списує gems з гаманця акаунту гравця; не вистачає — відмова з цифрами, нічого не списано.</summary>
        public async Task ChargeGemsAsync(Guid playerId, int cost, string reason, DateTime utcNow, CancellationToken cancellationToken)
        {
            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var player = await _players.GetByIdAsync(playerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", playerId);

            var wallet = await _wallets.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            if (wallet.GemBalance.Value < cost)
                throw new NotEnoughResourcesException("gems", cost, wallet.GemBalance.Value);

            wallet.SpendGems(new GemAmount(cost), reason, playerId, utcNow);
        }
    }
}
