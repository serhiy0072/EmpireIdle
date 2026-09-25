using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Marches.Queries
{
    /// <summary>
    /// Ворожі марші в дорозі, що стосуються гравця: на його село, на села соклановців
    /// і на споруди клану. Подія тривоги минає, а марш іде далі — після перезавантаження
    /// клієнт бере загрози звідси.
    /// </summary>
    public record GetIncomingAttacksQuery(Guid PlayerId) : IRequest<List<IncomingAttack>>, IPlayerScopedRequest;

    public sealed class GetIncomingAttacksQueryHandler : IRequestHandler<GetIncomingAttacksQuery, List<IncomingAttack>>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IMarchRepository _marchRepository;

        public GetIncomingAttacksQueryHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IMarchRepository marchRepository)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _marchRepository = marchRepository;
        }

        public async Task<List<IncomingAttack>> Handle(GetIncomingAttacksQuery request, CancellationToken cancellationToken)
        {
            var clanId = await _clanRepository.GetClanIdByMemberAsync(request.PlayerId, cancellationToken);

            // Поза кланом гравець захищає лише себе
            IReadOnlyCollection<Guid> defenders = clanId is { } id
                ? await _playerRepository.GetIdsByClanAsync(id, cancellationToken)
                : [request.PlayerId];

            return await _marchRepository.GetIncomingAttacksAsync(defenders, clanId, cancellationToken);
        }
    }
}
