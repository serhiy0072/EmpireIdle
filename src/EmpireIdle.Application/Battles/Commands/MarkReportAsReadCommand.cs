using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;

namespace EmpireIdle.Application.Battles.Commands
{
    /// <summary>Позначити звіт прочитаним.</summary>
    public record MarkReportAsReadCommand(Guid PlayerId, Guid ReportId) : IRequest, IPlayerScopedRequest;

    public sealed class MarkReportAsReadCommandHandler : IRequestHandler<MarkReportAsReadCommand>
    {
        private readonly IBattleReportRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public MarkReportAsReadCommandHandler(IBattleReportRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(MarkReportAsReadCommand request, CancellationToken cancellationToken)
        {
            var report = await _repository.GetByIdAsync(request.ReportId, cancellationToken)
                ?? throw new EntityNotFoundException("Report", request.ReportId);

            // 404, не 403: чужий звіт не відрізняється від неіснуючого — інакше id перебираються
            if (report.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Report", request.ReportId);

            report.MarkAsRead();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
