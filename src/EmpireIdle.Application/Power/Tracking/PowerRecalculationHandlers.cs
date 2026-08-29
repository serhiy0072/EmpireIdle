using EmpireIdle.Application.Common.Events;
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
}
