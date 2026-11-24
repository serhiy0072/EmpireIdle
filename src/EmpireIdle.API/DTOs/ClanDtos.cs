using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

// ---------- Запити ----------

/// <summary>Створення клану. Тег зберігається у верхньому регістрі.</summary>
public record CreateClanRequest(string Name, string Tag);

/// <summary>Опис і політика вступу. Потрібен дозвіл EditProfile.</summary>
public record UpdateClanSettingsRequest(string Description, ClanJoinPolicy JoinPolicy);

/// <summary>Нова роль для учасника. Роль лідера так не передається.</summary>
public record AssignClanRoleRequest(Guid RoleId);

/// <summary>
/// Власна роль клану: назва, ранг 0..100 і дозволи списком назв — так само, як вони приходять у ClanRoleResponse.
/// Потрібен дозвіл ManageRoles; видати можна лише ті дозволи, які маєш сам, і лише ранг нижчий за власний.
/// </summary>
public record ClanRoleRequest(string Name, int Rank, List<ClanPermission> Permissions);

/// <summary>Прохання про допомогу з таймером будівлі або тренування.</summary>
public record RequestClanHelpRequest(ClanHelpTarget TargetType, Guid TargetId);

/// <summary>Рішення по заявці або запрошенню.</summary>
public record ResolveClanRequestRequest(bool Approve);

// ---------- Відповіді ----------
