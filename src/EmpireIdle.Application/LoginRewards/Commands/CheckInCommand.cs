using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.LoginRewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.LoginRewards.Commands
{
    /// <summary>
    /// Вхід у гру (GDD §8.12): клієнт шле його при відкритті й після опівночі
    /// за UTC. Нагороди лягають листами в скриньку й згорають з початком
    /// наступного періоду. Нічного джоба немає навмисно: нагорода — за вхід,
    /// і той, хто не заходив, листа не отримує.
    /// </summary>
    public record CheckInCommand(Guid PlayerId) : IRequest<CheckInView>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CheckInCommandHandler : IRequestHandler<CheckInCommand, CheckInView>
    {
        private readonly ILoginRewardRepository _progressRepository;
        private readonly IMailRepository _mail;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CheckInCommandHandler> _logger;

        public CheckInCommandHandler(
            ILoginRewardRepository progressRepository,
            IMailRepository mail,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<CheckInCommandHandler> logger)
        {
            _progressRepository = progressRepository;
            _mail = mail;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<CheckInView> Handle(CheckInCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var today = LoginCalendar.Day(now);
            var config = _catalog.Config.LoginRewards;

            var progress = await _progressRepository.GetByPlayerAsync(request.PlayerId, cancellationToken);

            if (progress is null)
            {
                progress = new LoginRewardProgress(Guid.NewGuid(), request.PlayerId, _serverContext.ServerId);
                await _progressRepository.AddAsync(progress, cancellationToken);
            }

            var due = progress.CheckIn(today, config.Daily.Count);
            var letters = 0;

            if (due.DailyDay is { } day)
                letters += await SendAsync(request.PlayerId, MailKind.DailyReward, config.Daily[day - 1].Rewards, day,
                    now, LoginCalendar.DayEnds(today), cancellationToken);

            if (due.Weekly)
                letters += await SendAsync(request.PlayerId, MailKind.WeeklyReward, config.Weekly, null,
                    now, LoginCalendar.WeekEnds(today), cancellationToken);

            if (due.Monthly)
                letters += await SendAsync(request.PlayerId, MailKind.MonthlyReward, config.Monthly, null,
                    now, LoginCalendar.MonthEnds(today), cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (due.Any)
                _logger.LogInformation("Player {PlayerId} checked in: day {Day}, weekly {Weekly}, monthly {Monthly}.",
                    request.PlayerId, due.DailyDay, due.Weekly, due.Monthly);

            return new CheckInView(due.DailyDay, due.Weekly, due.Monthly, letters);
        }

        /// <summary>Лист із нагородою; порожній список нагород — листа немає.</summary>
        private async Task<int> SendAsync(Guid playerId, MailKind kind, List<RewardConfig> rewards, int? sequence,
            DateTime now, DateTime expiresAt, CancellationToken cancellationToken)
        {
            if (rewards.Count == 0)
                return 0;

            var letter = MailLetter.WithRewards(Guid.NewGuid(), _serverContext.ServerId, playerId, kind,
                rewards.Select(r => new MailReward(r.Type, r.Key, r.Amount)).ToList(), sequence, now, expiresAt);

            await _mail.AddLetterAsync(letter, cancellationToken);

            return 1;
        }
    }
}
