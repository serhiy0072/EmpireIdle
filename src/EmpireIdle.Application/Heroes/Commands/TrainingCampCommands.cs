using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Ставить героя в навчальний табір (GDD §6.1, рішення 07.10.2026): він отримує рівень опорної
    /// п'ятірки без витрати досвіду й лишається звичайним героєм — ходить у марші й данжі.
    /// </summary>
    public record PlaceHeroInCampCommand(Guid PlayerId, Guid HeroId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Виймає героя з табору: він повертається до власного рівня, слот перезаряджається.</summary>
    public record RemoveHeroFromCampCommand(Guid PlayerId, Guid HeroId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Знімає перезарядку слота за gems.</summary>
    /// <param name="Slot">Номер слота від 0.</param>
    public record SkipCampCooldownCommand(Guid PlayerId, int Slot) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Докуповує слот табору понад безкоштовні.</summary>
    public record BuyCampSlotCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class PlaceHeroInCampCommandHandler : IRequestHandler<PlaceHeroInCampCommand>
    {
        private readonly IHeroRepository _heroes;
        private readonly IVillageRepository _villages;
        private readonly VillageStatus _status;
        private readonly TrainingCampRules _rules;
        private readonly TrainingCampService _camps;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PlaceHeroInCampCommandHandler> _logger;

        public PlaceHeroInCampCommandHandler(
            IHeroRepository heroes,
            IVillageRepository villages,
            VillageStatus status,
            TrainingCampRules rules,
            TrainingCampService camps,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<PlaceHeroInCampCommandHandler> logger)
        {
            _heroes = heroes;
            _villages = villages;
            _status = status;
            _rules = rules;
            _camps = camps;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(PlaceHeroInCampCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var heroes = await _heroes.GetByPlayerAsync(request.PlayerId, cancellationToken);

            var hero = heroes.FirstOrDefault(h => h.Id == request.HeroId)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.CampSlot is not null)
                throw new RequirementNotMetException(RefusalReasons.CampHeroAlreadyIn, $"Hero {hero.Id} is already in the camp.");

            // Опорна п'ятірка рахується вже без цього героя: інакше він сам задав би собі рівень
            var outside = heroes.Where(h => h.CampSlot is null && h.Id != hero.Id).ToList();

            var level = _rules.CampLevel(outside)
                ?? throw new RequirementNotMetException(RefusalReasons.CampUnavailable,
                    $"The camp needs {_rules.ReferenceSize} heroes outside it, the player has {outside.Count}.",
                    _rules.ReferenceSize, outside.Count);

            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var camp = await _camps.GetOrCreateCampAsync(request.PlayerId, now, cancellationToken);
            var totalSlots = _rules.TotalSlots(_status.MainBuildingLevel(village), camp.PurchasedSlots);
            var occupied = heroes.Where(h => h.CampSlot is not null).Select(h => h.CampSlot!.Value).ToHashSet();

            var slot = camp.FirstFreeSlot(totalSlots, occupied, now)
                ?? throw new RequirementNotMetException(RefusalReasons.CampNoFreeSlot, "Every camp slot is taken or cooling down.");

            hero.EnterCamp(slot, level, now);

            // Герой міг бути в п'ятірці — тоді рівень табору зсувається для всіх у слотах
            _camps.Sync(heroes, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} entered camp slot {Slot} at level {Level}", hero.Id, slot, level);
        }
    }

    internal sealed class RemoveHeroFromCampCommandHandler : IRequestHandler<RemoveHeroFromCampCommand>
    {
        private readonly IHeroRepository _heroes;
        private readonly TrainingCampRules _rules;
        private readonly TrainingCampService _camps;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RemoveHeroFromCampCommandHandler> _logger;

        public RemoveHeroFromCampCommandHandler(
            IHeroRepository heroes,
            TrainingCampRules rules,
            TrainingCampService camps,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<RemoveHeroFromCampCommandHandler> logger)
        {
            _heroes = heroes;
            _rules = rules;
            _camps = camps;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RemoveHeroFromCampCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var heroes = await _heroes.GetByPlayerAsync(request.PlayerId, cancellationToken);

            var hero = heroes.FirstOrDefault(h => h.Id == request.HeroId)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            var slot = hero.LeaveCamp(now);

            var camp = await _camps.GetOrCreateCampAsync(request.PlayerId, now, cancellationToken);
            camp.StartCooldown(slot, _rules.SlotCooldown, now);

            // Герой повернувся до п'ятірки претендентів — рівень табору міг зрости
            _camps.Sync(heroes, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} left camp slot {Slot}", hero.Id, slot);
        }
    }

    internal sealed class SkipCampCooldownCommandHandler : IRequestHandler<SkipCampCooldownCommand>
    {
        private readonly TrainingCampRules _rules;
        private readonly TrainingCampService _camps;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public SkipCampCooldownCommandHandler(TrainingCampRules rules, TrainingCampService camps, IUnitOfWork unitOfWork,
            TimeProvider timeProvider)
        {
            _rules = rules;
            _camps = camps;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
        }

        public async Task Handle(SkipCampCooldownCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var camp = await _camps.GetOrCreateCampAsync(request.PlayerId, now, cancellationToken);

            // Перевірка до списання: за готовий слот gems не беремо
            if (!camp.IsCoolingDown(request.Slot, now))
                throw new RequirementNotMetException(RefusalReasons.CampSlotReady,
                    $"Camp slot {request.Slot} is not cooling down.", request.Slot + 1);

            await _camps.ChargeGemsAsync(request.PlayerId, _rules.SkipCooldownGems, $"camp-cooldown:{request.Slot}", now,
                cancellationToken);
            camp.SkipCooldown(request.Slot, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    internal sealed class BuyCampSlotCommandHandler : IRequestHandler<BuyCampSlotCommand>
    {
        private readonly TrainingCampRules _rules;
        private readonly TrainingCampService _camps;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public BuyCampSlotCommandHandler(TrainingCampRules rules, TrainingCampService camps, IUnitOfWork unitOfWork,
            TimeProvider timeProvider)
        {
            _rules = rules;
            _camps = camps;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
        }

        public async Task Handle(BuyCampSlotCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var camp = await _camps.GetOrCreateCampAsync(request.PlayerId, now, cancellationToken);

            var price = _rules.NextSlotPrice(camp.PurchasedSlots)
                ?? throw new RequirementNotMetException(RefusalReasons.CampAllSlotsBought,
                    "Every extra camp slot is already bought.", _rules.MaxPurchasableSlots);

            await _camps.ChargeGemsAsync(request.PlayerId, price, $"camp-slot:{camp.PurchasedSlots + 1}", now, cancellationToken);
            camp.AddPurchasedSlot(_rules.MaxPurchasableSlots, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
