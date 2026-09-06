using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Відкликає власні підкріплення з усіх союзних сіл. Часткове
    /// відкликання не передбачене: розділяти стек по одному юніту —
    /// це мікроменеджмент без ігрового сенсу.
    /// </summary>
    public record RecallReinforcementsCommand(Guid PlayerId)
        : IRequest<int>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Обробник RecallReinforcementsCommand.</summary>
    public sealed class RecallReinforcementsCommandHandler : IRequestHandler<RecallReinforcementsCommand, int>
    {
        private readonly ReinforcementReturner _returner;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RecallReinforcementsCommandHandler> _logger;

        public RecallReinforcementsCommandHandler(
            ReinforcementReturner returner,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<RecallReinforcementsCommandHandler> logger)
        {
            _returner = returner;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<int> Handle(RecallReinforcementsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var sent = await _returner.ReturnAllOfPlayerAsync(request.PlayerId, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} recalled reinforcements from {Count} villages",
                request.PlayerId, sent);

            return sent;
        }
    }
}
