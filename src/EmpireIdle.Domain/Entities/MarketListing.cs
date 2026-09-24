using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Лот ринку гравців: один предмет, герой або пачка за фіксовану ціну
    /// в золоті (GDD §8.8). Агрегат: купівля, зняття й закінчення строку
    /// змінюють лише його стан, а переміщення самого товару робить обробник.
    ///
    /// Паралельні покупки розводить токен xmin: друга отримає конфлікт,
    /// бо перша вже перевела лот у Sold.
    /// </summary>
    public class MarketListing : Entity
    {
        public int ServerId { get; private set; }

        public Guid SellerId { get; private set; }

        public MarketListingKind Kind { get; private set; }

        /// <summary>Екземпляр спорядження; лише для Kind = Equipment.</summary>
        public Guid? EquipmentId { get; private set; }

        /// <summary>Герой; лише для Kind = Hero.</summary>
        public Guid? HeroId { get; private set; }

        /// <summary>
        /// Ключ товару з конфіга: предмета, спорядження чи героя. Зберігається
        /// для будь-якого виду, щоб вітрину можна було фільтрувати без джойнів.
        /// </summary>
        public string ItemKey { get; private set; } = null!;

        /// <summary>Скільки штук у пачці; для спорядження й героя — 1.</summary>
        public int Quantity { get; private set; }

        /// <summary>
        /// Одиниць для ціни: Power на момент виставлення або кількість штук.
        /// Фіксується при виставленні — медіана рахується з того, за що
        /// реально продали, а не з того, чим предмет став пізніше.
        /// </summary>
        public double Units { get; private set; }

        /// <summary>Категорія ціни для медіани: «weapon», «hero.Rare», «item.teleport»…</summary>
        public string PricingKey { get; private set; } = null!;

        public int PriceGold { get; private set; }

        /// <summary>Сплачений податок — для історії; назад не повертається.</summary>
        public int TaxGold { get; private set; }

        public DateTime ListedAt { get; private set; }

        public DateTime ExpiresAt { get; private set; }

        public MarketListingState State { get; private set; }

        public Guid? BuyerId { get; private set; }

        /// <summary>Коли лот перестав бути активним: продаж, зняття чи кінець строку.</summary>
        public DateTime? ClosedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public MarketListing(Guid id, int serverId, Guid sellerId, MarketListingKind kind, Guid? equipmentId, Guid? heroId,
            string itemKey, int quantity, double units, string pricingKey, int priceGold, int taxGold,
            DateTime utcNow, TimeSpan duration) : base(id)
        {
            if (priceGold < 1)
                throw new ArgumentOutOfRangeException(nameof(priceGold), priceGold, "Price must be at least 1 gold.");

            if (quantity < 1)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be at least 1.");

            // Товар без одиниць не має ціни за одиницю — і в медіану ввійшов би діленням на нуль
            if (units <= 0)
                throw new ArgumentOutOfRangeException(nameof(units), units, "Units must be positive.");

            ServerId = serverId;
            SellerId = sellerId;
            Kind = kind;
            EquipmentId = equipmentId;
            HeroId = heroId;
            ItemKey = itemKey;
            Quantity = quantity;
            Units = units;
            PricingKey = pricingKey;
            PriceGold = priceGold;
            TaxGold = taxGold;
            ListedAt = utcNow;
            ExpiresAt = utcNow + duration;
            State = MarketListingState.Active;
            UpdatedAt = utcNow;
        }

        protected MarketListing() { } // Для EF Core

        /// <summary>Ціна за одиницю — те, що бачить медіана.</summary>
        public double PricePerUnit => PriceGold / Units;

        /// <summary>Чи можна купити просто зараз: активний і строк ще не минув.</summary>
        public bool IsOpenAt(DateTime utcNow) => State == MarketListingState.Active && utcNow < ExpiresAt;

        /// <summary>
        /// Продаж. Лот, чий строк минув, але сканер його ще не закрив,
        /// не продається: інакше рішення залежало б від того, як швидко
        /// дійшов джоб.
        /// </summary>
        public void Buy(Guid buyerId, DateTime utcNow)
        {
            if (!IsOpenAt(utcNow))
                throw new InvalidStateException(RefusalReasons.MarketListingClosed, $"Listing {Id} is no longer on sale.");

            if (buyerId == SellerId)
                throw new RequirementNotMetException(RefusalReasons.MarketOwnListing, $"Listing {Id} is the buyer's own.");

            State = MarketListingState.Sold;
            BuyerId = buyerId;
            Close(utcNow);
        }

        /// <summary>Продавець знімає лот. Податок не повертається — інакше коридор мацали б безкоштовно.</summary>
        public void Cancel(DateTime utcNow)
        {
            if (State != MarketListingState.Active)
                throw new InvalidStateException(RefusalReasons.MarketListingClosed, $"Listing {Id} is no longer active.");

            State = MarketListingState.Cancelled;
            Close(utcNow);
        }

        /// <summary>Строк минув. Викликає сканер; раніше строку — помилка викликача.</summary>
        public void Expire(DateTime utcNow)
        {
            if (State != MarketListingState.Active)
                throw new InvalidStateException($"Listing {Id} is no longer active.");

            if (utcNow < ExpiresAt)
                throw new InvalidStateException($"Listing {Id} expires at {ExpiresAt:O}, not yet.");

            State = MarketListingState.Expired;
            Close(utcNow);
        }

        private void Close(DateTime utcNow)
        {
            ClosedAt = utcNow;
            UpdatedAt = utcNow;
        }
    }
}
