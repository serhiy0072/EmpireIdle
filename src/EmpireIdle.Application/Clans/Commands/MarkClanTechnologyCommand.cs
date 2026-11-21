using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Technologies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Керівництво ставить мітку на технологію, у яку клан вкладається зараз (рішення 12.11): внесок у неї дає
    /// більші особисті нагороди. Мітка одна, нова замінює стару; Tier і Node null — зняти мітку.
    /// Позначити можна лише вузол, у який зараз приймають підношення.
    /// </summary>
    public record MarkClanTechnologyCommand(Guid PlayerId, int? Tier, string? Node)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class MarkClanTechnologyCommandHandler : IRequestHandler<MarkClanTechnologyCommand>
    {
        private readonly ClanOfferings _offerings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<MarkClanTechnologyCommandHandler> _logger;

        public MarkClanTechnologyCommandHandler(ClanOfferings offerings, IUnitOfWork unitOfWork, TimeProvider timeProvider,
            ILogger<MarkClanTechnologyCommandHandler> logger)
        {
            _offerings = offerings;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(MarkClanTechnologyCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _offerings.ClanOfAsync(request.PlayerId, cancellationToken);
            clan.EnsureCan(request.PlayerId, ClanPermission.ManageTechnologies);

            var research = await _offerings.ResearchOfAsync(clan, now, cancellationToken);

            TechnologyKey? key = null;

            if (request is { Tier: { } tier, Node: { } node })
            {
                key = new TechnologyKey(tier, node);
                _offerings.AvailableNode(research, key.Value);
            }

            research.Mark(key, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} marked {Technology} in clan {ClanId}",
                request.PlayerId, key?.ToString() ?? "nothing", clan.Id);
        }
    }
}
