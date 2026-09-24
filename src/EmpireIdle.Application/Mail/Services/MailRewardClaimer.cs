using EmpireIdle.Application.Mail.Contracts;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Mail.Services
{
    /// <summary>
    /// Забирає вкладення листа й видає його тим самим диспетчером, що й квести.
    /// Спільний для «Забрати» й «Забрати все», щоб вони не розійшлися.
    /// </summary>
    public sealed class MailRewardClaimer
    {
        private readonly RewardDispatcher _dispatcher;

        public MailRewardClaimer(RewardDispatcher dispatcher) => _dispatcher = dispatcher;

        public async Task<IReadOnlyList<MailRewardView>> ClaimAsync(MailLetter letter, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var rewards = letter.Claim(utcNow);

            // Лист показує точну нагороду й позначається отриманим —
            // повний склад не має тихо її з'їсти
            await _dispatcher.GrantAllAsync(letter.PlayerId,
                rewards.Select(r => new RewardConfig { Type = r.Type, Key = r.Key, Amount = r.Amount }),
                $"mail:{letter.Id}", utcNow, cancellationToken, ignoreStorageCap: true);

            return rewards.Select(r => new MailRewardView(r.Type, r.Key, r.Amount)).ToList();
        }

        /// <summary>Однакові нагороди з кількох листів складаються в один рядок.</summary>
        public static IReadOnlyList<MailRewardView> Sum(IEnumerable<MailRewardView> rewards)
            => rewards
                .GroupBy(r => (Type: r.Type.ToLowerInvariant(), r.Key))
                .Select(g => new MailRewardView(g.First().Type, g.Key.Key, g.Sum(r => r.Amount)))
                .ToList();
    }
}
