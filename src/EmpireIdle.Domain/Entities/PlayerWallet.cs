using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Гаманець гравця. Окремий Aggregate Root.
    /// Баланс змінюється ТІЛЬКИ через транзакції — ніколи напряму.
    /// </summary>
    public class PlayerWallet : Entity
    {
        private readonly List<WalletTransaction> _walletTransactions = new();
        private readonly List<IDomainEvent> _domainEvents = new();

        /// <summary>Ідентифікатор власника.</summary>
        public Guid PlayerId { get; private set; }

        /// <summary>Баланс gems (преміум валюта, купується за реальні гроші).</summary>
        public GemAmount GemBalance { get; private set; } = null!;


        /// <summary>Баланс coins (ігрова валюта, заробляється в грі).</summary>
        public CoinAmount CoinBalance { get; private set; } = null!;

        /// <summary>Історія транзакцій (тільки для читання).</summary>
        public IReadOnlyCollection<WalletTransaction> Transactions => _walletTransactions.AsReadOnly();

        /// <summary>Доменні події що очікують публікації.</summary>
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public PlayerWallet(Guid id, Guid playerId) : base(id)
        {
            PlayerId = playerId;
            GemBalance = GemAmount.Zero;
            CoinBalance = CoinAmount.Zero;
        }

        protected PlayerWallet() { } // для EF Core

        /// <summary>
        /// Нараховує gems після підтвердженої оплати через Stripe.
        /// </summary>
        /// <param name="amount">Кількість gems.</param>
        /// <param name="stripePaymentId">ID платежу в Stripe для idempotency.</param>
        public void AddGems(GemAmount amount, string stripePaymentId)
        {
            GemBalance = GemBalance.Add(amount);
            _walletTransactions.Add(new WalletTransaction(
                   Guid.NewGuid(),
                   Id,
                   TransactionType.GemPurchase,
                   amount.Value,
                   stripePaymentId));
            _domainEvents.Add(new GemsPurchased(PlayerId, amount, GemBalance));
        }

        /// <summary>
        /// Витрачає gems на внутрішньоігрові покупки.
        /// </summary>
        /// <param name="amount">Кількість gems.</param>
        /// <param name="description">Опис покупки.</param>
        public void SpendGems(GemAmount amount, string description)
        {
            GemBalance = GemBalance.Subtract(amount);
            _walletTransactions.Add(new WalletTransaction(
                Guid.NewGuid(),
                Id,
                TransactionType.GemSpend,
                -amount.Value,
                description
                )); 
        }

        /// <summary>Очищує список доменних подій після їх публікації.</summary>
        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
