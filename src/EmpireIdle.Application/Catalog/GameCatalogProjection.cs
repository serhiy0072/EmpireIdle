using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
                    building.Position is null ? null : new CatalogPosition(building.Position.X, building.Position.Y)))
                .ToList();

            var response = new CatalogResponse(
                heroes,
                items,
                resources,
                buildings,
                config.HeroSettings.Classes,
                config.HeroSettings.MaxConstellation,
                config.HeroSettings.MaxTier,
                Version: string.Empty);

            // Версія рахується з уже зібраної відповіді: змінився конфіг — змінився ETag
            return response with { Version = Hash(response) };
        }

        private static string Hash(CatalogResponse response)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(response);

            return Convert.ToHexString(SHA256.HashData(json), 0, 8).ToLowerInvariant();
        }
    }
}
