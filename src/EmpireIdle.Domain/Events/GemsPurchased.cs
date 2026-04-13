using EmpireIdle.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія що виникає коли гравець успішно купує gems через Stripe.
    /// </summary>
    public record GemsPurchased(Guid PlayerId, GemAmount Amount, GemAmount NewBalance) : IDomainEvent
    {
        /// <summary>Час коли подія відбулась.</summary>
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
