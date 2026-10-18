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
    public sealed record SquadMember(int Lane, string HeroClass, string? UnitType, FighterStats Hero, IReadOnlyList<Convoy> Convoys);

    /// <summary>Обидва загони на старті бою.</summary>
    public sealed record BattleSetup(IReadOnlyList<SquadMember> Attackers, IReadOnlyList<SquadMember> Defenders);
}
