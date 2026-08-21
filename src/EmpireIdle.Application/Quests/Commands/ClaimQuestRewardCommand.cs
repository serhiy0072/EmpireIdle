using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Commands
{
    /// <summary>Забрати нагороду за виконаний квест.</summary>
    public record ClaimQuestRewardCommand(Guid PlayerId, string QuestKey)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник: переводить квест у Claimed і видає нагороди.
    /// Перехід стану йде ПЕРЕД видачею — якщо квест уже забраний,
    /// нагорода не видається взагалі.
    /// </summary>
    public class ClaimQuestRewardCommandHandler : IRequestHandler<ClaimQuestRewardCommand>
    {
        private readonly IQuestRepository _questRepository;
        private readonly RewardDispatcher _rewards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<ClaimQuestRewardCommandHandler> _logger;

        public ClaimQuestRewardCommandHandler(
            IQuestRepository questRepository,
            RewardDispatcher rewards,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            ILogger<ClaimQuestRewardCommandHandler> logger)
        {
            _questRepository = questRepository;
            _rewards = rewards;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(ClaimQuestRewardCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var config = _catalog.Quest(request.QuestKey);

            if (config.Scope != QuestScope.Personal)
                throw new RequirementNotMetException($"Quest '{request.QuestKey}' is server-scoped — its rewards are granted on completion.");

            var progress = await _questRepository.GetAsync(request.PlayerId, request.QuestKey, cancellationToken)
                ?? throw new EntityNotFoundException("Quest progress", request.QuestKey);

            // Claim повертає false, якщо квест не завершений або вже забраний
            if (!progress.Claim(now))
                throw new InvalidStateException($"Quest '{request.QuestKey}' is not claimable (state: {progress.State}).");

            await _rewards.GrantAllAsync(request.PlayerId, config.Rewards, $"quest:{request.QuestKey}", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} claimed quest {QuestKey}", request.PlayerId, request.QuestKey);
        }
    }
}
