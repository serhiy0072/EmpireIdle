using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;

namespace EmpireIdle.Application.Mail.Commands
{
    /// <summary>Позначити особистий лист прочитаним. Повторна позначка нічого не змінює.</summary>
    public record MarkLetterReadCommand(Guid PlayerId, Guid LetterId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class MarkLetterReadCommandHandler : IRequestHandler<MarkLetterReadCommand>
    {
        private readonly IMailRepository _mail;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public MarkLetterReadCommandHandler(IMailRepository mail, IUnitOfWork unitOfWork, TimeProvider timeProvider)
        {
            _mail = mail;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
        }

        public async Task Handle(MarkLetterReadCommand request, CancellationToken cancellationToken)
        {
            var letter = await _mail.GetLetterAsync(request.LetterId, cancellationToken);

            // Чужий лист не відрізняється від неіснуючого
            if (letter is null || letter.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Mail letter", request.LetterId.ToString());

            letter.MarkRead(_timeProvider.GetUtcNow().UtcDateTime);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
