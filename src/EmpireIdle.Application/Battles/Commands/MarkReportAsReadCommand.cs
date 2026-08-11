using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;

namespace EmpireIdle.Application.Battles.Commands
{
    /// <summary>Позначити звіт прочитаним.</summary>
    public record MarkReportAsReadCommand(Guid PlayerId, Guid ReportId) : IRequest, IPlayerScopedRequest;

    public class MarkReportAsReadCommandHandler : IRequestHandler<MarkReportAsReadCommand>
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
                ?? throw new InvalidOperationException($"Report {request.ReportId} not found.");

            // Другий рубіж захисту: PlayerScopeBehavior перевіряє PlayerId у команді,
            // але сам звіт теж має належати цьому гравцю
            if ((report.PlayerId != request.PlayerId))
                throw new UnauthorizedAccessException("Report belongs to another player.");

            report.MarkAsRead();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
