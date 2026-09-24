using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Mail.Contracts;
using EmpireIdle.Application.Mail.Services;
using MediatR;

namespace EmpireIdle.Application.Mail.Commands
{
    /// <summary>
    /// Забрати вкладення з усіх листів разом (GDD §7.4). Нічого забирати —
    /// не помилка: кнопку могли натиснути в двох вкладках.
    /// </summary>
    public record ClaimAllRewardsCommand(Guid PlayerId) : IRequest<ClaimView>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class ClaimAllRewardsCommandHandler : IRequestHandler<ClaimAllRewardsCommand, ClaimView>
    {
        private readonly IMailRepository _mail;
        private readonly IUnitOfWork _unitOfWork;
        private readonly MailRewardClaimer _claimer;
        private readonly TimeProvider _timeProvider;

        public ClaimAllRewardsCommandHandler(IMailRepository mail, IUnitOfWork unitOfWork, MailRewardClaimer claimer,
            TimeProvider timeProvider)
        {
            _mail = mail;
            _unitOfWork = unitOfWork;
            _claimer = claimer;
            _timeProvider = timeProvider;
        }

        public async Task<ClaimView> Handle(ClaimAllRewardsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var letters = await _mail.GetClaimableLettersAsync(request.PlayerId, now, cancellationToken);
            var claimed = new List<MailRewardView>();

            foreach (var letter in letters)
                claimed.AddRange(await _claimer.ClaimAsync(letter, now, cancellationToken));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ClaimView(letters.Count, MailRewardClaimer.Sum(claimed));
        }
    }
}
