using EmpireIdle.Application.Research.ReadModels;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>
    /// Кланові технології для екрана: дерево з поступом і ціною підношень, стан підношень гравця, його монети
    /// й денний рейтинг клану. Дерево приходить із сервера, бо очки й доступність рахує він.
    /// </summary>
    /// <param name="Coins">Особисті кланові монети: лишаються при виході з клану.</param>
    public record ClanTechnologiesView(
        List<string> Tabs,
        List<ClanTechnologyTierView> Tiers,
        List<ClanTechnologyNodeView> Nodes,
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
    /// <param name="Resource">Ресурс підношення в цей вузол.</param>
    /// <param name="AttemptCost">Ціна однієї спроби ресурсом на цьому тірі.</param>
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
        int? NextPoints,
        string? Resource,
        int? AttemptCost);

    /// <summary>Підношення гравця зараз.</summary>
    /// <param name="NextAttemptAt">Коли відновиться наступна спроба; null — шкала повна.</param>
    /// <param name="GemPrice">Ціна наступного підношення за gems; null — денний ліміт вичерпано.</param>
    public record ClanOfferingStateView(
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
