using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Research.ReadModels
{
    /// <summary>
    /// Лабораторія гравця: дерево всіх тірів із поточними рівнями, ціною й часом наступного рівня,
    /// активне дослідження й сумарні бонуси. Дерево приходить із сервера, бо ціни й доступність рахує він.
    /// </summary>
    /// <param name="Unlocked">Лабораторію відкрито ратушею.</param>
    /// <param name="RequiredMainBuildingLevel">З якого рівня ратуші відкривається Лабораторія.</param>
    public record ResearchView(
        Guid? ResearchId,
        bool Unlocked,
        int RequiredMainBuildingLevel,
        List<string> Tabs,
        List<ResearchTierView> Tiers,
        List<ResearchNodeView> Nodes,
        ActiveResearchView? Active,
        List<TechnologyBonusView> Bonuses);

    /// <param name="Open">Гейти ратуші й світу вже виконано (передумови попереднього тіру — окремо на вузлах).</param>
    public record ResearchTierView(int Tier, int MainBuildingLevel, int WorldLevel, bool Open);

    /// <summary>Вузол на тірі. Next* — для наступного рівня; null, коли вузол уже на найвищому.</summary>
    /// <param name="Availability">TechnologyAvailability: Available, Maxed, TierGated, PreviousTierIncomplete, PrerequisitesMissing.</param>
    public record ResearchNodeView(
        int Tier,
        string Node,
        string Tab,
        int Row,
        List<string> Requires,
        int Level,
        int MaxLevel,
        string Availability,
        List<TechnologyEffectView> Effects,
        List<ResourceCost>? NextCost,
        int? NextSeconds);

    /// <summary>Ефект вузла на цьому тірі: бонус за рівень уже помножено на шкалу тіру.</summary>
    public record TechnologyEffectView(TechnologyStat Stat, string? Target, double PerLevel);

    /// <param name="PlannedSeconds">Повна тривалість на старті — від неї частка допомоги клану.</param>
    public record ActiveResearchView(int Tier, string Node, int Level, DateTime StartedAt, DateTime CompletesAt, int PlannedSeconds);

    /// <summary>Сумарний бонус технологій: стат, ціль (null — для всіх) і значення.</summary>
    public record TechnologyBonusView(TechnologyStat Stat, string? Target, double Value);
}
