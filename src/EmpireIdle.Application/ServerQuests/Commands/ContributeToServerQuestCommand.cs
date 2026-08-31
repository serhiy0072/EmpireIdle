using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.ServerQuests.Commands
{
    /// <summary>
    /// Записує внесок гравця в серверний квест.
    ///
    /// Пишемо ТІЛЬКИ у свій рядок гравця: спільний Total інкрементувався б
    /// усіма одночасно й став би точкою конкуренції для всього світу.
    /// Підсумок збирає джоб.
    /// </summary>
    public record ContributeToServerQuestCommand(Guid PlayerId, string QuestKey, long Amount) : IRequest;

    public sealed class ContributeToServerQuestCommandHandler : IRequestHandler<ContributeToServerQuestCommand>
    {
        private readonly IServerQuestRepository _questRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public ContributeToServerQuestCommandHandler(
            IServerQuestRepository questRepository,
            IUnitOfWork unitOfWork,
            IServerContext serverContext,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _questRepository = questRepository;
            _unitOfWork = unitOfWork;
            _serverContext = serverContext;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task Handle(ContributeToServerQuestCommand request, CancellationToken cancellationToken)
        {
            if (request.Amount <= 0)
                return;

            var config = _catalog.Quests.GetValueOrDefault(request.QuestKey);

            // Квест зник із конфіга або не серверний — подія просто не наша
            if (config is null || config.Scope != QuestScope.Server)
                return;

            var progress = await _questRepository.GetProgressAsync(request.QuestKey, cancellationToken);

            // Завершений квест внесків більше не приймає
            if (progress is not null && progress.State != QuestState.InProgress)
                return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var contribution = await _questRepository.GetContributionAsync(
                request.QuestKey, request.PlayerId, cancellationToken);

            if (contribution is null)
            {
                contribution = new ServerQuestContribution(
                    Guid.NewGuid(), _serverContext.ServerId, request.QuestKey, request.PlayerId);

                await _questRepository.AddContributionAsync(contribution, cancellationToken);
            }

            contribution.Add(request.Amount, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
