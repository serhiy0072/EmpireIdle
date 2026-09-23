using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Dungeons.Services
{
    /// <summary>
    /// Видає нагороду за успішний забіг: ресурси в село, один артефакт із
    /// набору данжу тієї рідкості, що відповідає рівню, і відмітку про
    /// зачистку — саме вона відкриває наступний рівень.
    /// </summary>
    public class DungeonRewarder
    {
        private readonly IDungeonRepository _dungeons;
        private readonly IVillageRepository _villages;
        private readonly ItemGranter _items;
        private readonly BattleBuilder _builder;
        private readonly GameCatalog _catalog;
        private readonly IRandomSource _random;

        public DungeonRewarder(
            IDungeonRepository dungeons,
            IVillageRepository villages,
            ItemGranter items,
            BattleBuilder builder,
            GameCatalog catalog,
            IRandomSource random)
        {
            _dungeons = dungeons;
            _villages = villages;
            _items = items;
            _builder = builder;
            _catalog = catalog;
            _random = random;
        }

        public async Task<DungeonReward> GrantAsync(DungeonRun run, DateTime now, CancellationToken cancellationToken)
        {
            var dungeon = _catalog.Dungeons.GetValueOrDefault(run.DungeonKey)
                ?? throw new EntityNotFoundException("Dungeon", run.DungeonKey);

            // Той самий множник, що показує вітрина: власна копія формули з іншим
            // запасним значенням давала б гравцю не ту нагороду, яку йому пообіцяли
            var multiplier = _builder.RewardMultiplier(run.Level);

            var resources = dungeon.Reward
                .Select(r => new ResourceCost { Resource = r.Resource, Amount = (int)Math.Round(r.Amount * multiplier) })
                .ToList();

            var village = await _villages.GetByPlayerIdAsync(run.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", run.PlayerId);

            village.GrantResources(resources, now);

            var artifacts = new List<string>();
            var rarity = RarityFor(run.Level);
            var setKey = $"{dungeon.ArtifactSetKey}_{rarity.ToString().ToLowerInvariant()}";

            var pieces = _catalog.Items.Values
                .Where(i => i.SetKey == setKey)
                .OrderBy(i => i.Key, StringComparer.Ordinal)
                .ToList();

            if (pieces.Count > 0)
            {
                // Одна частина за забіг: набір із чотирьох — це мета на кілька заходів
                var piece = pieces[_random.Next(pieces.Count)];

                await _items.GrantEquipmentAsync(run.PlayerId, piece, now, cancellationToken);

                artifacts.Add(piece.Key);
            }

            // Відмітка про зачистку відкриває наступний рівень; повтор того самого рівня нічого не додає
            var cleared = await _dungeons.GetClearedLevelsAsync(run.PlayerId, cancellationToken);

            if (cleared.GetValueOrDefault(dungeon.Key) < run.Level)
                await _dungeons.AddClearAsync(
                    new DungeonClear(Guid.NewGuid(), run.PlayerId, dungeon.Key, run.Level, now), cancellationToken);

            return new DungeonReward(resources, artifacts);
        }

        /// <summary>Рівень забігу обирає рідкість набору: 1 — звичайний, 2 — рідкісний, 3 — унікальний.</summary>
        public static Rarity RarityFor(int level) => level switch
        {
            <= 1 => Rarity.Common,
            2 => Rarity.Rare,
            _ => Rarity.Unique,
        };
    }
}
