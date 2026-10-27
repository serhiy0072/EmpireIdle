using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Відкриває скрині зброї з рюкзака для обраного героя (GDD §6.4): кожна дає фіксовані шматки
    /// його зброї. Герой має бути з трійки скрині й уже в ростері — шматки лежать на герої.
    /// Рівень зброї не піднімається сам: це окрема дія гравця.
    /// </summary>
    public record OpenWeaponChestsCommand(Guid PlayerId, Guid HeroId, string ItemKey, int Count)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class OpenWeaponChestsCommandHandler : IRequestHandler<OpenWeaponChestsCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<OpenWeaponChestsCommandHandler> _logger;

        public OpenWeaponChestsCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<OpenWeaponChestsCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(OpenWeaponChestsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var chest = _catalog.FindItem(request.ItemKey);
            if (chest is null || chest.Type != "weaponchest")
                throw new EntityNotFoundException("Weapon chest", request.ItemKey);

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Чужий герой не відрізняється від неіснуючого
            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (!chest.WeaponHeroes.Contains(hero.HeroKey))
                throw new RequirementNotMetException(RefusalReasons.HeroWeaponNotInChest,
                    $"Chest '{chest.Key}' does not hold weapon shards of '{hero.HeroKey}'.",
                    hero.HeroKey);

            // Зброя вже на стелі — шматки нікуди вкласти, і скрині згоріли б даремно
            var maxWeapon = _catalog.Config.HeroSettings.MaxWeaponLevel;
            if (hero.WeaponLevel >= maxWeapon)
                throw new RequirementNotMetException(RefusalReasons.HeroWeaponMaxed,
                    $"Hero {hero.Id} weapon is already +{hero.WeaponLevel}.", maxWeapon);

            var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, chest.Key, cancellationToken);

            // Нестача з цифрами, як і в інших витратах предметів; назву за ключем підставляє клієнт
            if (stack is null || stack.Count < request.Count)
                throw new NotEnoughResourcesException(chest.Key, request.Count, stack?.Count ?? 0);

            stack.Consume(request.Count);

            if (stack.Count == 0)
                _inventoryRepository.RemoveItem(stack);

            hero.AddWeaponShards(chest.WeaponShards * request.Count, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} opened {Count} × {Chest} for hero {HeroId}",
                request.PlayerId, request.Count, chest.Key, hero.Id);
        }
    }
}
