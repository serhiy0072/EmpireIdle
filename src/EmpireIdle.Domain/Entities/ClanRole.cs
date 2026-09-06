using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Роль у клані. Дані, а не enum: назви й набір ролей налаштовує сам клан,
    /// і «Генерал» в одному може бути «Маршалом» в іншому.
    ///
    /// Ранг відповідає на «кого можна чіпати» — не можна змінити роль тому,
    /// чий ранг ≥ твого. Саме він дає правило «офіцер не чіпає офіцерів»
    /// без окремого дозволу на кожен випадок.
    /// </summary>
    public class ClanRole : Entity
    {
        public Guid ClanId { get; private set; }

        public string Name { get; private set; } = null!;

        /// <summary>
        /// Старшинство, 0..100. Порівнюється, не додається — конкретні
        /// значення довільні, важливий лише порядок.
        /// </summary>
        public int Rank { get; private set; }

        public ClanPermission Permissions { get; private set; }

        /// <summary>
        /// Роль лідера. Видалити її не можна, і носій завжди рівно один:
        /// клан без лідера нікому розпустити чи передати.
        /// </summary>
        public bool IsLeaderRole { get; private set; }

        /// <summary>
        /// Роль за замовчуванням для нових учасників. Рівно одна на клан —
        /// інакше незрозуміло, куди зараховувати новачка.
        /// </summary>
        public bool IsDefaultRole { get; private set; }

        public ClanRole(Guid id, Guid clanId, string name, int rank, ClanPermission permissions,
            bool isLeaderRole = false, bool isDefaultRole = false) : base(id)
        {
            ClanId = clanId;
            Name = name;
            Rank = rank;
            Permissions = permissions;
            IsLeaderRole = isLeaderRole;
            IsDefaultRole = isDefaultRole;
        }

        protected ClanRole() { } // для EF Core

        public bool Can(ClanPermission permission) => (Permissions & permission) == permission;

        internal void Update(string name, int rank, ClanPermission permissions)
        {
            Name = name;
            Rank = rank;
            Permissions = permissions;
        }
    }
}
