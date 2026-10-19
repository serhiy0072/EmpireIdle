using EmpireIdle.Domain.Dungeons;

namespace EmpireIdle.Domain.Battles
{
    /// <summary>Сторона одномоментного бою: на арені нападник — той, хто натиснув «До бою», у вежі — гравець.</summary>
    public enum BattleSide
    {
        Attacker,
        Defender
    }

    /// <summary>Герой загону з його конвоями на одній доріжці.</summary>
    /// <param name="Lane">Доріжка, 0…SquadSize−1: від неї відстань до цілей.</param>
    /// <param name="HeroClass">Клас героя: фронт чи тил, ближній чи дальній бій.</param>
    /// <param name="UnitType">Тип юнітів ролі героя — для переваги типів; null — без переваги.</param>
    /// <param name="Skills">Відкриті вміння героя з вшитим рівнем (CombatSkill.Prepare); пасивки бій пропускає.</param>
    /// <param name="HeroKey">Ключ героя з heroes.json — щоб клієнт намалював, хто є хто.</param>
    public sealed record SquadMember(int Lane, string HeroClass, string? UnitType, FighterStats Hero, IReadOnlyList<Convoy> Convoys,
        IReadOnlyList<CombatSkill>? Skills = null, string? HeroKey = null);

    /// <summary>Наказ гравця в ручному бою: герой на доріжці Lane застосовує активне вміння на тику Tick.</summary>
    public sealed record ManualCast(int Tick, int Lane);

    /// <summary>Обидва загони на старті бою.</summary>
    /// <param name="AttackerCasts">
    /// Ручний бій нападника (рішення 19.10.2026): активні вміння його героїв не спрацьовують самі, лише за цими
    /// наказами. null — авто, усі вміння за перезарядкою. Періодичні вміння й захисник — завжди авто.
    /// </param>
    public sealed record BattleSetup(IReadOnlyList<SquadMember> Attackers, IReadOnlyList<SquadMember> Defenders,
        IReadOnlyList<ManualCast>? AttackerCasts = null);
}
