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
        private readonly List<WalletTransaction> _transactions = new();

        /// <summary>Ідентифікатор власника.</summary>
        public Guid PlayerId { get; private set; }

        /// <summary>Баланс gems (преміум валюта, купується за реальні гроші).</summary>
        public GemAmount GemBalance { get; private set; } = null!;


        /// <summary>Баланс coins (ігрова валюта, заробляється в грі).</summary>
        public CoinAmount CoinBalance { get; private set; } = null!;

        /// <summary>Історія транзакцій (тільки для читання).</summary>
        public IReadOnlyCollection<WalletTransaction> Transactions => _transactions.AsReadOnly();

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
            _transactions.Add(new WalletTransaction(
                   Guid.NewGuid(),
                   Id,
                   TransactionType.GemPurchase,
                   amount.Value,
                   stripePaymentId));
            RaiseDomainEvent(new GemsPurchased(PlayerId, amount, GemBalance));
        }

        /// <summary>
        /// Витрачає gems на внутрішньоігрові покупки.
        /// </summary>
        /// <param name="amount">Кількість gems.</param>
        /// <param name="description">Опис покупки.</param>
        public void SpendGems(GemAmount amount, string description)
        {
            GemBalance = GemBalance.Subtract(amount);
            _transactions.Add(new WalletTransaction(
                Guid.NewGuid(),
                Id,
                TransactionType.GemSpend,
                -amount.Value,
                description
                ));

            RaiseDomainEvent(new GemsSpent(PlayerId, amount, GemBalance, description));
        }

    }
}
