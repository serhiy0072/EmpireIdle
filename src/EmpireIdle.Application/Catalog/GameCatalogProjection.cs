using System.Collections.Concurrent;
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
        private readonly GameCatalog _catalog;

        /// <summary>Готова відповідь на кожну мову: будується раз, при першому запиті цією мовою.</summary>
        private readonly ConcurrentDictionary<string, CatalogResponse> _byLanguage = new();

        public GameCatalogProjection(GameCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Каталог мовою за замовчуванням — мовою самих конфігів.</summary>
        public CatalogResponse Response => ResponseFor(null);

        /// <summary>
        /// Каталог заданою мовою. Непідтримувана чи порожня мова — мова за
        /// замовчуванням: гравець бачить назви, а не помилку.
        /// </summary>
        public CatalogResponse ResponseFor(string? language)
        {
            var localization = _catalog.Config.Localization;

            var resolved = language is not null && localization.SupportedLanguages.Contains(language)
                ? language
                : localization.DefaultLanguage;

            return _byLanguage.GetOrAdd(resolved, lang => Build(_catalog, lang));
        }

        private static CatalogResponse Build(GameCatalog catalog, string language)
        {
            var config = catalog.Config;
            var names = config.Locales.GetValueOrDefault(language)?.Names ?? new Dictionary<string, string>();

            // Переклад поверх назви з конфіга; чого в локалі немає — показується як є
            string Name(string section, string key, string fallback) => names.GetValueOrDefault($"{section}.{key}", fallback);

            var heroes = config.Heroes
                .Select(hero => new CatalogHero(
                    hero.Key,
                    Name("hero", hero.Key, hero.DisplayName),
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
                    Name("item", item.Key, item.DisplayName),
                    item.Description,
                    item.Rarity.ToString(),
                    item.Type,
                    item.Slot?.ToString(),
                    item.WeaponClasses,
                    item.BaseStats,
                    item.SetKey,
                    item.ArtifactSlot,
                    item.PriceGold,
                    item.Tradeable,
                    item.Giftable))
                .ToList();

            var resources = config.Resources
                .Select(resource => new CatalogResource(resource.Key, Name("resource", resource.Key, resource.DisplayName), resource.Icon))
                .ToList();

            var buildings = config.Buildings
                .Select(building => new CatalogBuilding(
                    building.Key,
                    Name("building", building.Key, building.DisplayName),
                    building.ProducesResource,
                    building.Position is null ? null : new CatalogPosition(building.Position.X, building.Position.Y),
                    building.RequiresMainBuildingLevel))
                .ToList();

            var units = config.Units
                .Select(unit => new CatalogUnit(
                    unit.Key,
                    Name("unit", unit.Key, unit.DisplayName),
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
                ArtifactSets(config, Name),
                config.HeroSettings.Classes,
                config.HeroSettings.MaxConstellation,
                config.HeroSettings.MaxTier,
                config.MaxUnitLevel,
                config.Monetization.HealGemsPerUnit,
                config.Equipment.ArtifactSlots
                    .Select(slot => new CatalogArtifactSlot(slot.Key, Name("artifactSlot", slot.Key, slot.DisplayName)))
                    .ToList(),
                config.Equipment.MaxEnhancement,
                config.Equipment.RepairGemsBase,
                config.Equipment.RepairGemsPerLevel,
                new CatalogSpeedUp(
                    config.Monetization.SpeedUpFloorSeconds,
                    config.Monetization.SpeedUpFactor,
                    config.Monetization.SpeedUpExponent),
                config.Map.Width,
                catalog.MainBuildingKey,
                language,
                Version: string.Empty);

            // Версія рахується з уже зібраної відповіді: змінився конфіг — змінився ETag
            return response with { Version = Hash(response) };
        }

        /// <summary>
        /// Родини наборів у порядку рівнів. SetKey рідкості — <c>{Key}_{рідкість}</c>,
        /// та сама домовленість, за якою данж видає частину.
        /// </summary>
        private static List<CatalogArtifactSet> ArtifactSets(GameConfig config, Func<string, string, string, string> name)
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
                        name("artifactSet", set.Key, set.DisplayName),
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
