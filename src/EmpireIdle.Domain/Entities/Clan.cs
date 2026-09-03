using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Клан — об'єднання гравців одного світу.
    ///
    /// Ролі й членство живуть тут, а не окремими агрегатами: клан охороняє
    /// інваріанти складу — рівно один лідер, рівно одна роль за замовчуванням,
    /// не можна чіпати рівних або старших. Розкидані по хендлерах, вони
    /// трималися б на перевірках, які можна забути.
    /// </summary>
    public class Clan : Entity
    {
        private readonly List<ClanMember> _members = new();
        private readonly List<ClanRole> _roles = new();

        public int ServerId { get; private set; }

        public string Name { get; private set; } = null!;

        /// <summary>Коротка мітка для карти й чату.</summary>
        public string Tag { get; private set; } = null!;

        public string Description { get; private set; } = string.Empty;

        public ClanJoinPolicy JoinPolicy { get; private set; }

        /// <summary>Рівень клану — визначає ліміт учасників і кількість допомог.</summary>
        public int Level { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        public IReadOnlyCollection<ClanMember> Members => _members.AsReadOnly();

        public IReadOnlyCollection<ClanRole> Roles => _roles.AsReadOnly();

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        /// <summary>
        /// Створює клан зі стандартним набором ролей і засновником у ролі лідера.
        /// </summary>
        public Clan(Guid id, int serverId, string name, string tag, Guid founderId, DateTime utcNow) : base(id)
        {
            ServerId = serverId;
            Name = name;
            Tag = tag;
            Level = 1;
            JoinPolicy = ClanJoinPolicy.ByApproval;
            CreatedAt = utcNow;
            UpdatedAt = utcNow;

            _roles.AddRange(DefaultRoles(id));

            var leader = _roles.Single(r => r.IsLeaderRole);
            _members.Add(new ClanMember(Guid.NewGuid(), id, founderId, leader.Id, utcNow));
        }

        protected Clan() { } // для EF Core

        /// <summary>
        /// Стандартні ролі нового клану. Кроки рангу нерівні навмисно —
        /// між будь-якими двома лишається місце для нової ролі.
        /// </summary>
        private static List<ClanRole> DefaultRoles(Guid clanId) =>
        [
            new(Guid.NewGuid(), clanId, "Leader", 100,
                ClanPermission.Recruit | ClanPermission.Kick | ClanPermission.AssignRoles
                | ClanPermission.ManageRoles | ClanPermission.EditProfile
                | ClanPermission.BuildStructures | ClanPermission.Disband,
                isLeaderRole: true),

            new(Guid.NewGuid(), clanId, "Deputy", 85,
                ClanPermission.Recruit | ClanPermission.Kick | ClanPermission.AssignRoles
                | ClanPermission.ManageRoles | ClanPermission.EditProfile
                | ClanPermission.BuildStructures),

            new(Guid.NewGuid(), clanId, "General", 60,
                ClanPermission.Recruit | ClanPermission.Kick | ClanPermission.AssignRoles
                | ClanPermission.BuildStructures),

            new(Guid.NewGuid(), clanId, "Officer", 40,
                ClanPermission.Recruit | ClanPermission.Kick | ClanPermission.AssignRoles),

            new(Guid.NewGuid(), clanId, "Veteran", 20, ClanPermission.None),

            new(Guid.NewGuid(), clanId, "Member", 0, ClanPermission.None, isDefaultRole: true)
        ];

        // ---------- Читання ----------

        /// <summary>Роль гравця; null — не в клані.</summary>
        public ClanRole? RoleOf(Guid playerId)
        {
            var member = _members.FirstOrDefault(m => m.PlayerId == playerId);

            return member is null ? null : _roles.FirstOrDefault(r => r.Id == member.RoleId);
        }

        /// <summary>Скільки учасників вміщає клан на поточному рівні.</summary>
        public int Capacity(int baseCapacity, int perLevel) => baseCapacity + perLevel * (Level - 1);

        // ---------- Склад ----------

        /// <summary>Приймає гравця в роль за замовчуванням.</summary>
        public void Join(Guid playerId, int capacity, DateTime utcNow)
        {
            if (_members.Any(m => m.PlayerId == playerId))
                throw new AlreadyExistsException("Clan member", playerId.ToString());

            if (_members.Count >= capacity)
                throw new RequirementNotMetException($"The clan is full ({capacity} members).");

            var role = _roles.Single(r => r.IsDefaultRole);
            _members.Add(new ClanMember(Guid.NewGuid(), Id, playerId, role.Id, utcNow));

            Touch(utcNow);
        }

        /// <summary>Гравець виходить сам. Лідер спершу передає лідерство.</summary>
        public void Leave(Guid playerId, DateTime utcNow)
        {
            var member = _members.FirstOrDefault(m => m.PlayerId == playerId)
                ?? throw new EntityNotFoundException("Clan member", playerId);

            if (_roles.Single(r => r.Id == member.RoleId).IsLeaderRole)
                throw new InvalidStateException("Transfer leadership before leaving the clan.");

            _members.Remove(member);
            Touch(utcNow);
        }

        /// <summary>
        /// Виключає учасника. Потрібен дозвіл Kick і вищий ранг:
        /// офіцер не кікає офіцерів.
        /// </summary>
        public void Kick(Guid actorId, Guid targetId, DateTime utcNow)
        {
            var actor = RequireRole(actorId, ClanPermission.Kick);
            var target = _members.FirstOrDefault(m => m.PlayerId == targetId)
                ?? throw new EntityNotFoundException("Clan member", targetId);

            var targetRole = _roles.Single(r => r.Id == target.RoleId);

            if (targetRole.Rank >= actor.Rank)
                throw new RequirementNotMetException(
                    $"'{actor.Name}' cannot kick a '{targetRole.Name}'.");

            _members.Remove(target);
            Touch(utcNow);
        }

        /// <summary>
        /// Призначає роль. Не можна дати роль, рівну або вищу за власну,
        /// і не можна чіпати того, хто вже не нижчий за тебе.
        /// </summary>
        public void AssignRole(Guid actorId, Guid targetId, Guid roleId, DateTime utcNow)
        {
            var actor = RequireRole(actorId, ClanPermission.AssignRoles);

            var role = _roles.FirstOrDefault(r => r.Id == roleId)
                ?? throw new EntityNotFoundException("Clan role", roleId);

            if (role.IsLeaderRole)
                throw new RequirementNotMetException("Leadership is transferred, not assigned.");

            if (role.Rank >= actor.Rank)
                throw new RequirementNotMetException($"'{actor.Name}' cannot grant '{role.Name}'.");

            var target = _members.FirstOrDefault(m => m.PlayerId == targetId)
                ?? throw new EntityNotFoundException("Clan member", targetId);

            var currentRole = _roles.Single(r => r.Id == target.RoleId);

            if (currentRole.Rank >= actor.Rank)
                throw new RequirementNotMetException(
                    $"'{actor.Name}' cannot change the role of a '{currentRole.Name}'.");

            target.AssignRole(role.Id);
            Touch(utcNow);
        }

        /// <summary>
        /// Передає лідерство. Старий лідер стає другим за рангом, а не рядовим:
        /// передача часто тимчасова, і зниження до найнижчої її б карало.
        /// </summary>
        public void TransferLeadership(Guid fromPlayerId, Guid toPlayerId, DateTime utcNow)
        {
            var leaderRole = _roles.Single(r => r.IsLeaderRole);

            var current = _members.FirstOrDefault(m => m.PlayerId == fromPlayerId && m.RoleId == leaderRole.Id)
                ?? throw new RequirementNotMetException("Only the leader can transfer leadership.");

            var successor = _members.FirstOrDefault(m => m.PlayerId == toPlayerId)
                ?? throw new EntityNotFoundException("Clan member", toPlayerId);

            var secondHighest = _roles
                .Where(r => !r.IsLeaderRole)
                .OrderByDescending(r => r.Rank)
                .First();

            current.AssignRole(secondHighest.Id);
            successor.AssignRole(leaderRole.Id);

            Touch(utcNow);
        }

        // ---------- Ролі ----------

        /// <summary>Створює роль. Ранг не може дорівнювати власному або перевищувати його.</summary>
        public Guid CreateRole(Guid actorId, string name, int rank, ClanPermission permissions, DateTime utcNow)
        {
            var actor = RequireRole(actorId, ClanPermission.ManageRoles);

            EnsureRankBelow(actor, rank);
            EnsureNameFree(name, exceptRoleId: null);

            var role = new ClanRole(Guid.NewGuid(), Id, name, rank, permissions);
            _roles.Add(role);

            Touch(utcNow);
            return role.Id;
        }

        /// <summary>Змінює роль. Роль лідера незмінна — інакше її можна знеправити.</summary>
        public void UpdateRole(Guid actorId, Guid roleId, string name, int rank, ClanPermission permissions,
            DateTime utcNow)
        {
            var actor = RequireRole(actorId, ClanPermission.ManageRoles);

            var role = _roles.FirstOrDefault(r => r.Id == roleId)
                ?? throw new EntityNotFoundException("Clan role", roleId);

            if (role.IsLeaderRole)
                throw new RequirementNotMetException("The leader role cannot be edited.");

            EnsureRankBelow(actor, role.Rank);
            EnsureRankBelow(actor, rank);
            EnsureNameFree(name, exceptRoleId: roleId);

            role.Update(name, rank, permissions);
            Touch(utcNow);
        }

        /// <summary>
        /// Видаляє роль. Її носії спускаються на найближчу нижчу за рангом —
        /// не на найнижчу: втратити роль не має означати впасти на дно.
        /// </summary>
        /// <returns>Скільки учасників перепризначено.</returns>
        public int DeleteRole(Guid actorId, Guid roleId, DateTime utcNow)
        {
            var actor = RequireRole(actorId, ClanPermission.ManageRoles);

            var role = _roles.FirstOrDefault(r => r.Id == roleId)
                ?? throw new EntityNotFoundException("Clan role", roleId);

            if (role.IsLeaderRole)
                throw new RequirementNotMetException("The leader role cannot be deleted.");

            if (role.IsDefaultRole)
                throw new RequirementNotMetException("The default role cannot be deleted — new members need one.");

            EnsureRankBelow(actor, role.Rank);

            var fallback = _roles
                .Where(r => r.Id != roleId && r.Rank < role.Rank)
                .OrderByDescending(r => r.Rank)
                .FirstOrDefault()
                ?? _roles.Single(r => r.IsDefaultRole);

            var affected = _members.Where(m => m.RoleId == roleId).ToList();

            foreach (var member in affected)
                member.AssignRole(fallback.Id);

            _roles.Remove(role);
            Touch(utcNow);

            return affected.Count;
        }

        // ---------- Профіль і активність ----------

        public void UpdateSettings(Guid actorId, string description, ClanJoinPolicy joinPolicy, DateTime utcNow)
        {
            RequireRole(actorId, ClanPermission.EditProfile);

            Description = description;
            JoinPolicy = joinPolicy;

            Touch(utcNow);
        }

        /// <summary>Фіксує активність учасника — за нею визначається мертвий лідер.</summary>
        public void RecordActivity(Guid playerId, DateTime utcNow)
        {
            _members.FirstOrDefault(m => m.PlayerId == playerId)?.Touch(utcNow);
            Touch(utcNow);
        }

        // ---------- Внутрішнє ----------

        /// <summary>Роль виконавця з перевіркою дозволу.</summary>
        private ClanRole RequireRole(Guid actorId, ClanPermission permission)
        {
            var role = RoleOf(actorId)
                ?? throw new RequirementNotMetException("Only clan members can do that.");

            if (!role.Can(permission))
                throw new RequirementNotMetException($"'{role.Name}' lacks the {permission} permission.");

            return role;
        }

        /// <summary>
        /// Ранг має бути строго нижчий за ранг виконавця: інакше будь-хто
        /// з ManageRoles створив би собі рівню або старшу роль і обійшов ієрархію.
        /// </summary>
        private static void EnsureRankBelow(ClanRole actor, int rank)
        {
            if (rank >= actor.Rank)
                throw new RequirementNotMetException(
                    $"'{actor.Name}' cannot manage roles at rank {rank} or above.");
        }

        private void EnsureNameFree(string name, Guid? exceptRoleId)
        {
            if (_roles.Any(r => r.Id != exceptRoleId && r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new AlreadyExistsException("Clan role", name);
        }

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
    }
}
