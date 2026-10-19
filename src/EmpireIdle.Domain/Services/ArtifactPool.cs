using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Звідки береться випадковий артефакт рідкості — для скринь і для нагород вежі однаково: з наборів
    /// данжів, які ратуша гравця вже відкрила; жоден не відкритий — набір найпершого данжу, щоб новачок
    /// не отримав порожнечу.
    /// </summary>
    public static class ArtifactPool
    {
        /// <summary>Ключі артефактів у сталому порядку: порядок у JSON не впливає на те, що дає той самий кидок.</summary>
        public static IReadOnlyList<string> Pieces(GameCatalog catalog, Rarity rarity, int mainBuildingLevel)
        {
            var suffix = rarity.ToString().ToLowerInvariant();

            // Валідатор гарантує, що бодай один данж має набір цієї рідкості
            var dungeons = catalog.Dungeons.Values
                .Where(d => catalog.SetPieces.ContainsKey($"{d.ArtifactSetKey}_{suffix}"))
                .ToList();

            var open = dungeons.Where(d => d.RequiresMainBuildingLevel <= mainBuildingLevel).ToList();

            if (open.Count == 0 && dungeons.Count > 0)
            {
                var first = dungeons.Min(d => d.RequiresMainBuildingLevel);
                open = dungeons.Where(d => d.RequiresMainBuildingLevel == first).ToList();
            }

            return open
                .SelectMany(d => catalog.SetPieces[$"{d.ArtifactSetKey}_{suffix}"])
                .Distinct()
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
        }
    }
}
