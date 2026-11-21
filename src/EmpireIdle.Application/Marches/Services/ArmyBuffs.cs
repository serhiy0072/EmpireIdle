using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Бонуси війська в армійському бою: вміння й спорядження героїв плюс бойові технології власника.
    /// Технології — власника кожного стека: союзник у моєму селі б'ється під своїми, не моїми,
    /// і діють вони навіть тоді, коли героя при війську немає.
    /// </summary>
    public sealed class ArmyBuffs
    {
        private readonly HeroCombatModifiers _modifiers;
        private readonly HeroCombatants _combatants;
        private readonly PlayerTechnologies _technologies;

        public ArmyBuffs(HeroCombatModifiers modifiers, HeroCombatants combatants, PlayerTechnologies technologies)
        {
            _modifiers = modifiers;
            _combatants = combatants;
            _technologies = technologies;
        }

        /// <summary>Бонус війська одного гравця під проводом цих героїв (марш, табір, підкріплення).</summary>
        public async Task<StackBuff> ForArmyAsync(Guid playerId, IReadOnlyCollection<Hero?> heroes,
            CancellationToken cancellationToken)
        {
            var gear = await _combatants.GearAsync(heroes, cancellationToken);

            return _modifiers.ForMarch(heroes, gear).With(await _technologies.ForPlayerAsync(playerId, cancellationToken));
        }

        /// <summary>
        /// Бонуси оборони гарнізону: захисники господаря ведуть його юнітів, герої кожного союзника — лише
        /// його стек (GDD §6.1). Союзник без героя теж отримує свої технології — власників беремо зі стеків.
        /// </summary>
        /// <param name="hostPlayerId">Господар гарнізону; null — споруда клану, де господаря немає.</param>
        public async Task<DefenceBuffs> ForDefenceAsync(Guid? hostPlayerId, IReadOnlyCollection<Hero?> hostHeroes,
            IReadOnlyCollection<Hero> stationed, IEnumerable<DefenceStack> defence, CancellationToken cancellationToken)
        {
            var gear = await _combatants.GearAsync(stationed.Concat(hostHeroes), cancellationToken);

            var host = hostPlayerId is { } hostId
                ? _modifiers.ForMarch(hostHeroes, gear).With(await _technologies.ForPlayerAsync(hostId, cancellationToken))
                : StackBuff.None;

            var heroesByOwner = stationed
                .Where(h => h.PlayerId != hostPlayerId)
                .GroupBy(h => h.PlayerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var owners = heroesByOwner.Keys
                .Concat(defence.Select(s => s.OwnerPlayerId).OfType<Guid>())
                .Where(id => id != hostPlayerId)
                .Distinct()
                .ToList();

            var allies = new Dictionary<Guid, StackBuff>();

            foreach (var owner in owners)
            {
                var heroes = heroesByOwner.GetValueOrDefault(owner) ?? [];
                var technologies = await _technologies.ForPlayerAsync(owner, cancellationToken);

                allies[owner] = _modifiers.ForMarch(heroes, gear).With(technologies);
            }

            return new DefenceBuffs(host, allies);
        }
    }
}
