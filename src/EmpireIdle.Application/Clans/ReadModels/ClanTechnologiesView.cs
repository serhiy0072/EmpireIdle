using EmpireIdle.Application.Research.ReadModels;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>
    /// Кланові технології для екрана: дерево з поступом, запас очок, стан підношень гравця, його монети
    /// й денний рейтинг клану. Дерево приходить із сервера, бо очки й доступність рахує він.
    /// </summary>
    /// <param name="UnallocatedPoints">Запас клану, який офіцер ще не розподілив.</param>
    /// <param name="Coins">Особисті кланові монети: лишаються при виході з клану.</param>
    public record ClanTechnologiesView(
        List<string> Tabs,
        List<ClanTechnologyTierView> Tiers,
        List<ClanTechnologyNodeView> Nodes,
        long UnallocatedPoints,
        long Coins,
        List<TechnologyBonusView> Bonuses,
        ClanOfferingStateView Offering,
        ClanDailyContributionView Daily,
        List<ClanContributorView> Contributors);

    /// <param name="Open">Рівень світу вже дозволяє цей тір (передумови попереднього тіру — окремо на вузлах).</param>
    public record ClanTechnologyTierView(int Tier, int WorldLevel, bool Open);

    /// <summary>Вузол на тірі. NextPoints — скільки всього коштує наступний рівень; null, коли вузол уже на найвищому.</summary>
    /// <param name="Availability">TechnologyAvailability: Available, Maxed, TierGated, PreviousTierIncomplete, PrerequisitesMissing.</param>
    /// <param name="Progress">Уже вкладено в наступний рівень.</param>
    public record ClanTechnologyNodeView(
        int Tier,
        string Node,
        string Tab,
        int Row,
        List<string> Requires,
        int Level,
        int MaxLevel,
        string Availability,
        List<TechnologyEffectView> Effects,
        int Progress,
        int? NextPoints);

    /// <summary>Підношення гравця зараз.</summary>
    /// <param name="Resource">Ресурс, який клан приймає зараз; клієнт шле саме його, щоб зсув черги дав відмову, а не несподіване списання.</param>
    /// <param name="ResourceCost">Ціна спроби за нинішньої ратуші гравця.</param>
    /// <param name="OfferingsUntilRotation">Скільки підношень ресурсом клану до зміни ресурсу.</param>
    /// <param name="NextAttemptAt">Коли відновиться наступна спроба; null — шкала повна.</param>
    /// <param name="GemPrice">Ціна наступного підношення за gems; null — денний ліміт вичерпано.</param>
    public record ClanOfferingStateView(
        string? Resource,
        int? ResourceCost,
        long OfferingsUntilRotation,
        int Attempts,
        int MaxAttempts,
        DateTime? NextAttemptAt,
        int PointsPerAttempt,
        int? GemPrice,
        int GemOfferingsToday,
        int GemDailyLimit,
        int GemPoints);

    /// <summary>Денний внесок гравця й пороги нагород; лічиться за добу UTC у всіх кланах разом.</summary>
    public record ClanDailyContributionView(int PointsToday, List<ClanDailyRewardView> Rewards);

    /// <param name="Reached">Гравець уже перетнув цей поріг сьогодні, нагороду видано.</param>
    public record ClanDailyRewardView(int Points, List<RewardConfig> Rewards, bool Reached);

    /// <summary>Рядок денного рейтингу клану.</summary>
    public record ClanContributorView(Guid PlayerId, string Name, long Today, long Total);
}
