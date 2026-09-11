using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Завершує одне дозріле замовлення. Одиниця роботи сканера:
    /// конфлікт паралелізму коштує цього героя, а не весь прогін.
    /// </summary>
    public record CompleteHeroLevelUpCommand(Guid OrderId) : IRequest;

    public sealed class CompleteHeroLevelUpCommandHandler : IRequestHandler<CompleteHeroLevelUpCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CompleteHeroLevelUpCommandHandler> _logger;

        public CompleteHeroLevelUpCommandHandler(
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<CompleteHeroLevelUpCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CompleteHeroLevelUpCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var order = await _heroRepository.GetOrderByIdAsync(request.OrderId, cancellationToken);
            if (order is null || order.CompletesAt > now)
                return;

            var hero = await _heroRepository.GetByIdAsync(order.HeroId, cancellationToken);

            // Героя могли видалити між вибіркою й обробкою — замовлення прибираємо,
            // інакше воно висітиме в черзі назавжди
            if (hero is null)
            {
                _heroRepository.RemoveOrder(order);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            // Стеля перевірялась на постановці; тут беремо цільовий рівень
            // із самого замовлення, бо ратуша могла впасти в рівні не могла,
            // а тір за цей час не змінюється
            hero.GainLevel(order.TargetLevel, now);

            _heroRepository.RemoveOrder(order);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} reached level {Level}", hero.Id, hero.Level);
        }
    }
}
