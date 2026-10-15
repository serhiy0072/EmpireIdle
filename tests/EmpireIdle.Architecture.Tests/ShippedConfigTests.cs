using AwesomeAssertions;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// JSON не перевіряється компілятором, тож помилка в конфігу доживає до старту.
/// Program читає конфіг ще до контейнера, тому на дизайн-таймі це виглядає як
/// падіння Add-Migration без жодного повідомлення.
///
/// Ці тести прив'язують справжні файли так само, як це робить Program, і ганяють
/// по них ту саму валідацію.
/// </summary>
public class ShippedConfigTests
{
    private static string ApiProject(string root) => Path.Combine(root, "src", "EmpireIdle.API");

    /// <summary>
    /// Шляхи з усіх викликів AddJsonFile у Program.cs. Читаються з коду, а не
    /// дублюються тут: інакше доданий конфіг лишився б поза перевіркою.
    /// </summary>
    private static List<string> ConfigFiles(string root)
    {
        var program = File.ReadAllText(Path.Combine(ApiProject(root), "Program.cs"));

        return Regex.Matches(program, @"AddJsonFile\(\s*""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    private static GameConfig Bind(string root)
    {
        var builder = new ConfigurationBuilder().SetBasePath(ApiProject(root));

        foreach (var file in ConfigFiles(root))
            builder.AddJsonFile(file, optional: false);

        return builder.Build().GetSection("GameConfig").Get<GameConfig>(o => o.ErrorOnUnknownConfiguration = true)
            ?? throw new InvalidOperationException("GameConfig section is missing from the shipped config.");
    }

    /// <summary>
    /// Запобіжник для самих тестів: якщо регулярка перестане знаходити файли,
    /// решта проходитиме на порожньому конфігу й не перевірятиме нічого.
    /// </summary>
    [Fact]
    public void ConfigFileList_ShouldBeReadFromProgram()
    {
        var files = ConfigFiles(RepositoryRoot.Find());

        files.Should().NotBeEmpty(
            "список читається з Program.cs — порожній означає, що тести конфіга нічого не перевіряють");
        files.Should().Contain(f => f.Contains("items.json", StringComparison.Ordinal));
    }

    /// <summary>
    /// Прив'язка ловить те, чого не ловить компілятор: рядок, який не лягає
    /// в enum, число замість рядка, зайвий рівень вкладеності.
    /// </summary>
    [Fact]
    public void ShippedConfig_ShouldBindToGameConfig()
    {
        var config = Bind(RepositoryRoot.Find());

        config.Resources.Should().NotBeEmpty();
        config.Buildings.Should().NotBeEmpty();
        config.Units.Should().NotBeEmpty();
        config.Items.Should().NotBeEmpty();
    }

    /// <summary>
    /// Каталог валідує узгодженість між секціями й будує словники, тобто ловить
    /// і дублікати ключів, і посилання в нікуди — але вже на справжніх даних.
    /// </summary>
    [Fact]
    public void ShippedConfig_ShouldBuildTheCatalog()
    {
        var config = Bind(RepositoryRoot.Find());

        var build = () => new GameCatalog(config);

        build.Should().NotThrow();
    }

    /// <summary>
    /// Кожен тип нагороди в конфізі має зареєстрованого грантера.
    ///
    /// Список грантерів у DI ведеться руками, тож новий тип легко додати
    /// в JSON і забути в коді. Тоді помилка спливає не на старті, а тоді,
    /// коли перший гравець дійде до цієї нагороди — RewardDispatcher кидає
    /// на невідомому типі вже під час видачі.
    /// </summary>
    [Fact]
    public void ShippedConfig_ShouldOnlyUseSupportedRewardTypes()
    {
        var config = Bind(RepositoryRoot.Find());

        var supported = typeof(IRewardGranter).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IRewardGranter)))
            .Select(t => (string)t.GetProperty(nameof(IRewardGranter.RewardType))!
                .GetValue(RuntimeHelpers.GetUninitializedObject(t))!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var used = config.Quests
            .SelectMany(q => q.Rewards)
            .Select(r => r.Type)
            .Distinct()
            .ToList();

        used.Should().NotBeEmpty("інакше тест проходить на порожньому списку й нічого не перевіряє");

        used.Should().OnlyContain(type => supported.Contains(type),
            $"кожен тип нагороди потребує грантера; зареєстровані: {string.Join(", ", supported)}");
    }

    /// <summary>
    /// Спорядження не видається як стаковий предмет.
    ///
    /// ItemRewardGranter кладе PlayerItem — рядок «ключ плюс кількість».
    /// Артефакт так не видається: йому потрібен власний екземпляр зі статами
    /// й журналом роллів. Помилка мовчазна: нагорода наче видана, а предмета
    /// в героя немає й не буде.
    /// </summary>
    [Fact]
    public void ShippedConfig_ShouldNotGrantEquipmentAsAStackableItem()
    {
        var config = Bind(RepositoryRoot.Find());
        var catalog = new GameCatalog(config);

        var wrong = config.Quests
            .SelectMany(q => q.Rewards)
            .Where(r => string.Equals(r.Type, "Item", StringComparison.OrdinalIgnoreCase) && r.Key is not null)
            .Where(r => catalog.Items.GetValueOrDefault(r.Key!)?.Slot is not null)
            .Select(r => r.Key!)
            .ToList();

        wrong.Should().BeEmpty("спорядження видається типом Equipment, а не Item");
    }

    /// <summary>
    /// За рівних рівня, тіру, зірок і зброї найслабший унікальний сильніший за найсильнішого
    /// рідкісного, а той — за найсильнішого звичайного (рішення 15.10.2026). Тір і зірки множать
    /// усі стати однаково, а бонус зброї росте з рідкістю, тож досить перевірити власні стати —
    /// на першому й останньому рівні, бо сила лінійна від рівня.
    /// </summary>
    [Fact]
    public void ShippedHeroes_ShouldBeOrderedByRarityInPower()
    {
        var config = Bind(RepositoryRoot.Find());
        var progression = new HeroProgression(config.HeroSettings);

        foreach (var level in new[] { 1, config.HeroSettings.MaxLevel })
        {
            double Power(HeroConfig hero) => hero.BaseStats.Keys.Sum(stat => config.HeroSettings.StatPowerWeight(stat)
                * progression.StatValue(hero, stat, level, hero.NativeTier, hero.NativeTier, starParts: 0, weaponLevel: 0));

            var byRank = config.Heroes
                .GroupBy(h => h.Rank)
                .ToDictionary(g => g.Key, g => g.Select(Power).ToList());

            byRank[Rarity.Unique].Min().Should().BeGreaterThan(byRank[Rarity.Rare].Max(),
                $"на рівні {level} найслабший унікальний герой мусить бути сильнішим за найсильнішого рідкісного");
            byRank[Rarity.Rare].Min().Should().BeGreaterThan(byRank[Rarity.Common].Max(),
                $"на рівні {level} найслабший рідкісний герой мусить бути сильнішим за найсильнішого звичайного");
        }
    }

    /// <summary>
    /// Набори по класах (рішення 15.10.2026): на кожному рівні в кожного класу героїв рівно один набір,
    /// і кожен падає зі свого данжу. Без цього якийсь клас на тірі лишився б без спорядження,
    /// а гравцеві довелося б фармити «чужий» лут.
    /// </summary>
    [Fact]
    public void ShippedArtifactSets_ShouldGiveEveryClassOneSetPerTier_FromItsOwnDungeon()
    {
        var config = Bind(RepositoryRoot.Find());
        var sets = config.Equipment.ArtifactSets;

        foreach (var tier in sets.Select(s => s.Tier).Distinct())
        {
            sets.Where(s => s.Tier == tier).Select(s => s.Class).Should().BeEquivalentTo(config.HeroSettings.Classes,
                $"на рівні {tier} у кожного класу має бути рівно один набір");
        }

        config.Dungeons.Dungeons.Select(d => d.ArtifactSetKey).Should().BeEquivalentTo(sets.Select(s => s.Key),
            "кожен набір падає рівно з одного данжу");
    }
}
