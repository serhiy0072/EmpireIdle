using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Безкоштовні відкриття ключової скрині гравця (рішення 12.10.2026): не більше FreePerDay за добу UTC
    /// і не частіше, ніж раз на cooldown. Рідкісна — 5 на добу з перезарядкою 5 хв, унікальна —
    /// 1 на добу з перезарядкою 48 год, тобто раз на два дні. Понад це скриню відкриває ключ.
    /// </summary>
    public class KeyChestProgress : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Ключ скрині (таблиця в Lootboxes).</summary>
        public string ChestKey { get; private set; } = null!;

        /// <summary>Доба UTC, за яку рахується <see cref="FreeOnDay"/>; null — безкоштовних ще не було.</summary>
        public DateOnly? FreeDay { get; private set; }

        /// <summary>Скільки безкоштовних відкриттів було в добу <see cref="FreeDay"/>.</summary>
        public int FreeOnDay { get; private set; }

        /// <summary>Коли було останнє безкоштовне відкриття — від нього рахується перезарядка.</summary>
        public DateTime? LastFreeAt { get; private set; }

        /// <summary>Токен паралелізму: два кліки у двох вкладках інакше взяли б одне безкоштовне двічі.</summary>
        public uint Version { get; private set; }

        public KeyChestProgress(Guid id, Guid playerId, string chestKey) : base(id)
        {
            PlayerId = playerId;
            ChestKey = chestKey;
        }

        protected KeyChestProgress() { } // Для EF Core

        /// <summary>Скільки безкоштовних уже взято цієї доби UTC: нова доба починає з нуля.</summary>
        public int FreeUsedToday(DateTime utcNow)
            => FreeDay == DateOnly.FromDateTime(utcNow) ? FreeOnDay : 0;

        /// <summary>
        /// Коли буде наступне безкоштовне відкриття; null — уже можна. Вичерпана доба відкладає його
        /// до опівночі UTC, але не раніше кінця перезарядки.
        /// </summary>
        public DateTime? NextFreeAt(int perDay, int cooldownMinutes, DateTime utcNow)
        {
            if (perDay <= 0)
                return DateTime.MaxValue;

            var cooled = LastFreeAt?.AddMinutes(cooldownMinutes);
            var next = FreeUsedToday(utcNow) >= perDay ? utcNow.Date.AddDays(1) : (DateTime?)null;

            if (cooled is { } at && at > utcNow && (next is null || at > next))
                next = at;

            return next;
        }

        /// <summary>Бере безкоштовне відкриття; ще не можна — відмова з моментом, коли буде.</summary>
        public void UseFree(int perDay, int cooldownMinutes, DateTime utcNow)
        {
            if (NextFreeAt(perDay, cooldownMinutes, utcNow) is { } next)
                throw new InvalidStateException(RefusalReasons.KeyChestFreeNotReady,
                    $"Free opening of '{ChestKey}' is ready at {next:O}.", next.ToString("O"));

            FreeOnDay = FreeUsedToday(utcNow) + 1;
            FreeDay = DateOnly.FromDateTime(utcNow);
            LastFreeAt = utcNow;
        }
    }
}
