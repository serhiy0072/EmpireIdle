using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Beasts.Services
{
    /// <summary>
    /// Приручення (GDD §5.10). Правила перевіряються двічі: при відправці — щоб армія
    /// не йшла даремно, і на прибутті — бо за час маршу звіринець міг заповнитись.
    /// Дві копії цих правил розійшлися б.
    /// </summary>
    public sealed class BeastTamer
    {
        private readonly IBeastPenRepository _pens;
        private readonly IMonsterRepository _monsters;
        private readonly IRandomSource _random;
        private readonly BeastTaming _taming;
        private readonly VillageCapacities _capacities;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;

        public BeastTamer(IBeastPenRepository pens, IMonsterRepository monsters, IRandomSource random,
            BeastTaming taming, VillageCapacities capacities, VillageStatus status, GameCatalog catalog)
        {
            _pens = pens;
            _monsters = monsters;
            _random = random;
            _taming = taming;
            _capacities = capacities;
            _status = status;
            _catalog = catalog;
        }

        /// <summary>Кидає на першій невиконаній умові: гравець має побачити причину до відправки.</summary>
        public async Task EnsureAllowedAsync(Village village, Guid monsterId, CancellationToken cancellationToken)
        {
            var monster = await _monsters.GetByIdAsync(monsterId, cancellationToken)
                ?? throw new EntityNotFoundException("Monster", monsterId);

            var beast = _taming.ForMonster(monster.Type)
                ?? throw new RequirementNotMetException(RefusalReasons.BeastNotTameable,
                    $"Monster '{monster.Type}' cannot be tamed.");

            var slots = _capacities.BeastSlots(village, _status);

            if (slots == 0)
                throw new RequirementNotMetException(RefusalReasons.BeastPenMissing, "There is no beast pen to keep a beast.");

            var pen = await _pens.GetByPlayerReadOnlyAsync(village.PlayerId, cancellationToken);

            if (pen is not null && !pen.HasRoomFor(beast.Key, slots))
                throw new RequirementNotMetException(RefusalReasons.BeastPenFull,
                    $"The beast pen holds {slots} kinds of beasts and they are all taken.", slots);
        }

        /// <summary>
        /// Перемога з наміром «Приручити»: кидок шансу, гарантія й місце.
        /// Повертає ключ звіра, якщо перемога його дала; null — гравець отримує звичайну здобич.
        /// </summary>
        public async Task<string?> TameAsync(Village village, string monsterType, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (_taming.ForMonster(monsterType) is not { } beast)
                return null;

            var slots = _capacities.BeastSlots(village, _status);

            // Без місць звіра нема куди селити — і порожній звіринець не створюємо
            if (slots == 0)
                return null;

            var rolled = _random.NextDouble() < _taming.ChanceFor(beast, _capacities.BeastPenLevel(village, _status));

            var pen = await _pens.GetByPlayerAsync(village.PlayerId, cancellationToken);

            if (pen is null)
            {
                pen = new BeastPen(Guid.NewGuid(), village.PlayerId, village.ServerId, utcNow);
                await _pens.AddAsync(pen, cancellationToken);
            }

            var settings = _catalog.Config.Beasts;
            var outcome = pen.ResolveTaming(beast.Key, rolled, settings.PityWins, slots, settings.MaxRank, utcNow);

            return outcome is TameOutcome.Tamed or TameOutcome.RankedUp ? beast.Key : null;
        }
    }
}
