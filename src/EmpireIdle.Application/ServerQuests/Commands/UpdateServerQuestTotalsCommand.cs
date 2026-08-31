using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.ServerQuests.Commands
{
    /// <summary>
    /// Перераховує спільні суми серверних квестів із внесків.
    ///
    /// Гаряча стрічка: гравці пишуть кожен у свій рядок без конфліктів,
    /// а цей джоб раз на N секунд збирає підсумок в один. Затримка в
    /// кілька секунд для лічильника на весь світ непомітна.
    /// </summary>
    public record UpdateServerQuestTotalsCommand : IRequest;

    public sealed class UpdateServerQuestTotalsCommandHandler : IRequestHandler<UpdateServerQuestTotalsCommand>
    {
        private readonly IServerQuestRepository _questRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UpdateServerQuestTotalsCommandHandler> _logger;

        public UpdateServerQuestTotalsCommandHandler(
            IServerQuestRepository questRepository,
            IUnitOfWork unitOfWork,
            IServerContext serverContext,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<UpdateServerQuestTotalsCommandHandler> logger)
        {
            _questRepository = questRepository;
            _unitOfWork = unitOfWork;
            _serverContext = serverContext;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(UpdateServerQuestTotalsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Квести з конфіга, яких ще немає в базі, створюються тут:
            // окремого кроку ініціалізації світу не треба
            await EnsureProgressRowsAsync(cancellationToken);

            var active = await _questRepository.GetActiveAsync(cancellationToken);

            foreach (var progress in active)
            {
                var total = await _questRepository.SumContributionsAsync(progress.QuestKey, cancellationToken);

                if (progress.UpdateTotal(total, now))
                    _logger.LogInformation("Server quest {QuestKey} completed at {Total}", progress.QuestKey, total);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureProgressRowsAsync(CancellationToken cancellationToken)
        {
            var serverQuests = _catalog.Config.Quests.Where(q => q.Scope == QuestScope.Server);

            foreach (var config in serverQuests)
            {
                if (await _questRepository.GetProgressAsync(config.Key, cancellationToken) is not null)
                    continue;

                var target = config.Objectives.Sum(o => (long)o.Count);

                await _questRepository.AddProgressAsync(
                    new ServerQuestProgress(Guid.NewGuid(), _serverContext.ServerId, config.Key, target),
                    cancellationToken);
            }
        }
    }
}
