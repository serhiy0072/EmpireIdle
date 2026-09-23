using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Catalog
{
    /// <summary>
    /// Будує відповідь каталогу один раз за запуск процесу.
    ///
    /// Конфіг незмінний після старту, тож перебирати словники на кожен запит
    /// нема сенсу. Синглтон, а не статичне поле: інакше тести ділили б
    /// один каталог на всі кейси.
    /// </summary>
    public class GameCatalogProjection
    {
        private readonly Lazy<CatalogResponse> _response;

        public GameCatalogProjection(GameCatalog catalog)
        {
            _response = new Lazy<CatalogResponse>(() => Build(catalog));
        }

        public CatalogResponse Response => _response.Value;

        private static CatalogResponse Build(GameCatalog catalog)
        {
            var config = catalog.Config;

            var heroes = config.Heroes
                .Select(hero => new CatalogHero(
                    hero.Key,
                    hero.DisplayName,
                    hero.Class,
                    hero.Rank.ToString(),
                    hero.Description,
                    hero.Speed ?? config.HeroSettings.DefaultMarchSpeed,
                    hero.SummonShards,
                    hero.ShardPriceGold,
                    hero.BaseStats,
                    hero.StatGrowth,
                    hero.Passives
                        .Select(passive => new CatalogPassive(
                            passive.Key,
                            passive.DisplayName,
                            passive.UnlockConstellation,
                            passive.Target,
                            passive.Stat,
                            passive.BasePercent,
                            passive.PercentPerConstellation))
                        .ToList()))
                .ToList();

            var items = config.Items
                .Select(item => new CatalogItem(
                    item.Key,
                    item.DisplayName,
                    item.Description,
                    item.Rarity.ToString(),
                    item.Type,
                    item.Slot?.ToString(),
                    item.WeaponClasses,
                    item.BaseStats,
                    item.SetKey,
                    item.PriceGold))
                .ToList();

            var resources = config.Resources
                .Select(resource => new CatalogResource(resource.Key, resource.DisplayName, resource.Icon))
                .ToList();

            var buildings = config.Buildings
                .Select(building => new CatalogBuilding(
                    building.Key,
                    building.DisplayName,
                    building.ProducesResource,
                    building.Position is null ? null : new CatalogPosition(building.Position.X, building.Position.Y),
                    building.RequiresMainBuildingLevel))
                .ToList();

            var units = config.Units
                .Select(unit => new CatalogUnit(
                    unit.Key,
                    unit.DisplayName,
                    unit.RequiresBuilding,
                    unit.RequiresBuildingLevel,
                    unit.BaseTrainMinutes,
                    unit.LevelUpCostGrowth,
                    unit.Cost.Select(cost => new CatalogUnitCost(cost.Resource, cost.Amount)).ToList()))
                .ToList();

            var response = new CatalogResponse(
                heroes,
                items,
                resources,
                buildings,
                units,
                ArtifactSets(config),
                config.HeroSettings.Classes,
                config.HeroSettings.MaxConstellation,
                config.HeroSettings.MaxTier,
                config.MaxUnitLevel,
                config.Monetization.HealGemsPerUnit,
                config.Equipment.ArtifactSlots,
                config.Equipment.MaxEnhancement,
                config.Equipment.RepairGemsBase,
                config.Equipment.RepairGemsPerLevel,
                config.Map.Width,
                catalog.MainBuildingKey,
                Version: string.Empty);

            // Версія рахується з уже зібраної відповіді: змінився конфіг — змінився ETag
            return response with { Version = Hash(response) };
        }

        /// <summary>
        /// Родини наборів у порядку рівнів. SetKey рідкості — <c>{Key}_{рідкість}</c>,
        /// та сама домовленість, за якою данж видає частину.
        /// </summary>
        private static List<CatalogArtifactSet> ArtifactSets(GameConfig config)
        {
            var equipment = config.Equipment;
            var multipliers = equipment.ArtifactTierMultipliers;

            return equipment.ArtifactSets
                .OrderBy(set => set.Tier)
                .ThenBy(set => set.Key, StringComparer.Ordinal)
                .Select(set =>
                {
                    var dungeon = config.Dungeons.Dungeons.FirstOrDefault(d => d.ArtifactSetKey == set.Key);

                    var rarities = Enum.GetValues<Rarity>()
                        .Select(rarity =>
                        {
                            var setKey = $"{set.Key}_{rarity.ToString().ToLowerInvariant()}";
                            var bonus = equipment.SetBonuses.FirstOrDefault(b => b.SetKey == setKey);

                            return new CatalogSetRarity(
                                rarity.ToString(),
                                setKey,
                                bonus?.RequiredPieces ?? 0,
                                bonus?.Stats ?? new Dictionary<string, double>(),
                                config.Items
                                    .Where(item => item.SetKey == setKey)
                                    .Select(item => item.Key)
                                    .OrderBy(key => key, StringComparer.Ordinal)
                                    .ToList());
                        })
                        .ToList();

                    return new CatalogArtifactSet(
                        set.Key,
                        set.DisplayName,
                        set.Tier,
                        multipliers.Count == 0 ? 1.0 : multipliers[Math.Clamp(set.Tier - 1, 0, multipliers.Count - 1)],
                        set.FocusStats,
                        dungeon is null ? null : new CatalogSetSource(dungeon.Key, dungeon.DisplayName, dungeon.RequiresMainBuildingLevel),
                        rarities);
                })
                .ToList();
        }

        private static string Hash(CatalogResponse response)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(response);

            return Convert.ToHexString(SHA256.HashData(json), 0, 8).ToLowerInvariant();
        }
    }
}
