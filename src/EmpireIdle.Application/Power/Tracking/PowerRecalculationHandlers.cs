using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Power.Commands;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Power.Tracking
{
    /// <summary>
    /// Підписники на події, що змінюють армію. Кожен лише переадресовує
    /// команду перерахунку: подія каже «перерахуй», а не «додай N» —
    /// дельти дають розсинхрон, який виявиться через місяць.
    ///
    /// Три окремі класи замість узагальненого: події не мають спільного
    /// інтерфейсу, а закриті типи MediatR знаходить сам, без реєстрації
    /// рефлексією.
    /// </summary>
    public sealed class RecalculatePowerOnUnitsTrained
        : INotificationHandler<DomainEventNotification<UnitsTrained>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnUnitsTrained(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<UnitsTrained> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePowerCommand(notification.DomainEvent.GarrisonId), cancellationToken);
    }

    public sealed class RecalculatePowerOnBattleFought
        : INotificationHandler<DomainEventNotification<BattleFought>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnBattleFought(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<BattleFought> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePowerCommand(notification.DomainEvent.GarrisonId), cancellationToken);
    }

    public sealed class RecalculatePowerOnMarchReturned
        : INotificationHandler<DomainEventNotification<MarchReturned>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnMarchReturned(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<MarchReturned> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePowerCommand(notification.DomainEvent.GarrisonId), cancellationToken);
    }

    /// <summary>Рівень, тір, сузір'я чи новий герой — сила ростера змінилась.</summary>
    public sealed class RecalculatePowerOnHeroChanged
        : INotificationHandler<DomainEventNotification<HeroChanged>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnHeroChanged(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<HeroChanged> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePlayerPowerCommand(notification.DomainEvent.PlayerId), cancellationToken);
    }

    /// <summary>Вдягли, зняли, заточили чи прокачали — сила спорядження змінилась.</summary>
    public sealed class RecalculatePowerOnEquipmentChanged
        : INotificationHandler<DomainEventNotification<EquipmentChanged>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnEquipmentChanged(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<EquipmentChanged> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePlayerPowerCommand(notification.DomainEvent.PlayerId), cancellationToken);
    }

    /// <summary>Вивчений рівень особистої технології.</summary>
    public sealed class RecalculatePowerOnResearchCompleted
        : INotificationHandler<DomainEventNotification<ResearchCompleted>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnResearchCompleted(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<ResearchCompleted> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePlayerPowerCommand(notification.DomainEvent.PlayerId), cancellationToken);
    }

    /// <summary>Вступ, вихід чи кік: сила кланових технологій додалась або зникла.</summary>
    public sealed class RecalculatePowerOnClanMembershipChanged
        : INotificationHandler<DomainEventNotification<ClanMembershipChanged>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnClanMembershipChanged(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<ClanMembershipChanged> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePlayerPowerCommand(notification.DomainEvent.PlayerId), cancellationToken);
    }

    /// <summary>Клан закрив рівень: перераховуємо кожного учасника. Подія рідкісна, а клан обмежений місткістю.</summary>
    public sealed class RecalculatePowerOnClanTechnologyLevelsCompleted
        : INotificationHandler<DomainEventNotification<ClanTechnologyLevelsCompleted>>
    {
        private readonly IClanRepository _clans;
        private readonly IMediator _mediator;

        public RecalculatePowerOnClanTechnologyLevelsCompleted(IClanRepository clans, IMediator mediator)
        {
            _clans = clans;
            _mediator = mediator;
        }

        public async Task Handle(DomainEventNotification<ClanTechnologyLevelsCompleted> notification, CancellationToken cancellationToken)
        {
            var clan = await _clans.GetByIdAsync(notification.DomainEvent.ClanId, cancellationToken);

            if (clan is null)
                return;

            foreach (var playerId in clan.Members.Select(m => m.PlayerId).ToList())
                await _mediator.Send(new RecalculatePlayerPowerCommand(playerId), cancellationToken);
        }
    }

    public sealed class RecalculatePowerOnReinforcementsMoved
        : INotificationHandler<DomainEventNotification<ReinforcementsMoved>>
    {
        private readonly IMediator _mediator;

        public RecalculatePowerOnReinforcementsMoved(IMediator mediator) => _mediator = mediator;

        public Task Handle(DomainEventNotification<ReinforcementsMoved> notification, CancellationToken cancellationToken)
            => _mediator.Send(new RecalculatePowerCommand(notification.DomainEvent.OwnerGarrisonId), cancellationToken);
    }
}
