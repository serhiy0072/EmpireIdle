using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    public class WalletTransaction : Entity
    {
        /// <summary>Ідентифікатор гаманця.</summary>
        public Guid WalletId { get; private set; }

        /// <summary>Тип транзакції.</summary>
        public TransactionType Type { get; private set; }

        /// <summary>Сума (від'ємна для витрат).</summary>
        public int Amount { get; private set; }

        /// <summary>Опис або Stripe Payment ID.</summary>
        public string Reference { get; private set; } = null!;

        /// <summary>Час транзакції.</summary>
        public DateTime CreatedAt { get; private set; }

        public WalletTransaction(Guid id, Guid walletId, TransactionType type,
            int amount, string reference) : base(id)
        {
            WalletId = walletId;
            Type = type;
            Amount = amount;
            Reference = reference;
            CreatedAt = DateTime.UtcNow;
        }

        protected WalletTransaction() { } // для EF Core
    }
}
