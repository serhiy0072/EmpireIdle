using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Обробляє походи, чий час прибуття настав.</summary>
    public record CompleteDueMarchesCommand : IRequest;

    public class CompleteDueMarchesCommandHandler : IRequestHandler<CompleteDueMarchesCommand>
    {
        private const int ServerId = 1;

        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly MarchCalculator _calculator;
        private readonly ILogger<CompleteDueMarchesCommandHandler> _logger;

        public CompleteDueMarchesCommandHandler(
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            MarchCalculator calculator,
            ILogger<CompleteDueMarchesCommandHandler> logger)
        {
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _logger = logger;
        }

        public async Task Handle(CompleteDueMarchesCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var due = await _marchRepository.GetDueAsync(now, cancellationToken);

            if (due.Count == 0)
                return;

            var arrived = 0;
            var returned = 0;

            foreach (var march in due)
            {
                if (march.State == MarchState.Outbound)
                {
                    // TODO (Phase 12): тут відбудеться бій — поки армія просто розвертається
                    var units = march.GetUnits();
                    var backDuration = _calculator.CalculateDuration(
                        ServerId, march.TargetX, march.TargetY, march.OriginX, march.OriginY, units);

                    march.TurnBack(backDuration);
                    arrived++;
                }
                else if (march.State == MarchState.Returning)
                {
                    var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken);
                    if (garrison is null)
                    {
                        _logger.LogWarning("Garrison {GarrisonId} not found for march {MarchId}", march.GarrisonId, march.Id);
                        continue;
                    }

                    garrison.ReceiveUnits(march.GetUnits());
                    march.Complete();
                    returned++;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Marches processed: {Arrived} arrived, {Returned} returned home", arrived, returned);
        }
    }
}