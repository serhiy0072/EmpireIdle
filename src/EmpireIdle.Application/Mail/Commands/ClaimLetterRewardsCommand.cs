using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Mail.Contracts;
using EmpireIdle.Application.Mail.Services;
using EmpireIdle.Domain.Exceptions;
using MediatR;

namespace EmpireIdle.Application.Mail.Commands
{
    /// <summary>Забрати вкладення одного листа.</summary>
    public record ClaimLetterRewardsCommand(Guid PlayerId, Guid LetterId)
        : IRequest<ClaimView>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class ClaimLetterRewardsCommandHandler : IRequestHandler<ClaimLetterRewardsCommand, ClaimView>
    {
        private readonly IMailRepository _mail;
        private readonly IUnitOfWork _unitOfWork;
        private readonly MailRewardClaimer _claimer;
        private readonly TimeProvider _timeProvider;

        public ClaimLetterRewardsCommandHandler(IMailRepository mail, IUnitOfWork unitOfWork, MailRewardClaimer claimer,
            TimeProvider timeProvider)
        {
            _mail = mail;
            _unitOfWork = unitOfWork;
            _claimer = claimer;
            _timeProvider = timeProvider;
        }

        public async Task<ClaimView> Handle(ClaimLetterRewardsCommand request, CancellationToken cancellationToken)
        {
            var letter = await _mail.GetLetterAsync(request.LetterId, cancellationToken);

            // Чужий лист не відрізняється від неіснуючого
            if (letter is null || letter.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Mail letter", request.LetterId.ToString());

            var rewards = await _claimer.ClaimAsync(letter, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ClaimView(1, rewards);
        }
    }
}
