namespace EmpireIdle.Application.Mail.Contracts
{
    /// <summary>Що гравець отримав: скільки листів забрано й сумарні нагороди.</summary>
    public record ClaimView(int Letters, IReadOnlyList<MailRewardView> Rewards);
}
