using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities;

/// <summary>
/// Гаманець гравця. Окремий Aggregate Root.
/// Баланс змінюється ТІЛЬКИ через транзакції — ніколи напряму.
/// </summary>
public class PlayerWallet : Entity
{
    private readonly List<WalletTransaction> _transactions = new();

    /// <summary>
    /// Ідентифікатор акаунта-власника (IdentityUser.Id). Gems глобальні для акаунта,
    /// тому гаманець не прив'язаний до Player на конкретному сервері.
    /// </summary>
    public string UserId { get; private set; } = null!;

    /// <summary>Баланс gems (преміум валюта, купується за реальні гроші).</summary>
    public GemAmount GemBalance { get; private set; } = null!;


    /// <summary>Історія транзакцій (тільки для читання).</summary>
    public IReadOnlyCollection<WalletTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>
    /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
    /// правились лише дочірні рядки — інакше токен паралелізму на корені
    /// не спрацював би, бо EF не оновив би рядок кореня.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    public PlayerWallet(Guid id, string userId) : base(id)
    {
        UserId = userId;
        GemBalance = GemAmount.Zero;
    }

    protected PlayerWallet() { } // для EF Core

    /// <summary>
    /// Нараховує gems після підтвердженої оплати через Stripe.
    /// </summary>
    /// <param name="amount">Кількість gems.</param>
    /// <param name="reference">ID платежу в Stripe для idempotency.</param>
    /// <param name="notifyPlayerId">Гравець, у чию SignalR-групу піде подія про баланс.</param>
    public void AddGems(GemAmount amount, string reference, Guid notifyPlayerId, DateTime utcNow)
    {
        GemBalance = GemBalance.Add(amount);
        _transactions.Add(new WalletTransaction( Guid.NewGuid(), Id, TransactionType.GemPurchase, amount.Value, reference, utcNow));

        RaiseDomainEvent(new GemsPurchased(notifyPlayerId, amount, GemBalance, utcNow));
        Touch(utcNow);
    }

    /// <summary>
    /// Витрачає gems на внутрішньоігрові покупки.
    /// </summary>
    /// <param name="amount">Кількість gems.</param>
    /// <param name="description">Опис покупки.</param>
    /// <param name="notifyPlayerId">Гравець, у чию SignalR-групу піде подія про баланс.</param>
    public void SpendGems(GemAmount amount, string description, Guid notifyPlayerId, DateTime utcNow)
    {
        GemBalance = GemBalance.Subtract(amount);
        _transactions.Add(new WalletTransaction( Guid.NewGuid(), Id, TransactionType.GemSpend, -amount.Value, description, utcNow));

        RaiseDomainEvent(new GemsSpent(notifyPlayerId, amount, GemBalance, description, utcNow));
        Touch(utcNow);
    }

    private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
}

