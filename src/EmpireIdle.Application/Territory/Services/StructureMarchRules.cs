using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Application.Territory.Services
{
    /// <summary>
    /// Хто куди може йти маршем щодо кланової споруди: свою — будувати й тримати
    /// гарнізоном, чужу — атакувати. Живе окремо від правил підкріплень села:
    /// посольства, щита й господаря в споруди немає.
    /// </summary>
    public sealed class StructureMarchRules
    {
        private readonly IClanRepository _clanRepository;

        public StructureMarchRules(IClanRepository clanRepository) => _clanRepository = clanRepository;

        public async Task EnsureAllowedAsync(Guid playerId, ClanStructure structure, MarchIntent intent,
            CancellationToken cancellationToken)
        {
            var clanId = await _clanRepository.GetClanIdByMemberAsync(playerId, cancellationToken);
            var own = clanId == structure.ClanId;

            if (intent == MarchIntent.Reinforce && !own)
                throw new RequirementNotMetException(RefusalReasons.TerritoryForeignStructure,
                    "Only members of the owning clan can build or garrison a structure.");

            if (intent == MarchIntent.Attack && own)
                throw new RequirementNotMetException(RefusalReasons.TerritoryOwnStructure,
                    "A clan cannot attack its own structure.");
        }
    }
}
