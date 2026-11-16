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

/// <summary>Рядок списку кланів.</summary>
public record ClanListItemResponse(
    Guid Id,
    string Name,
    string Tag,
    string Description,
    string JoinPolicy,
    int MemberCount,
    int Capacity,
    bool IsFull);

/// <summary>Сторінка списку кланів.</summary>
public record ClanListResponse(List<ClanListItemResponse> Items, int Total, int Page, int PageSize);

/// <summary>Картка чужого клану — без складу.</summary>
public record ClanProfileResponse(
    Guid Id,
    string Name,
    string Tag,
    string Description,
    string JoinPolicy,
    int MemberCount,
    int Capacity,
    bool IsFull,
    DateTime CreatedAt);

/// <summary>
/// Роль клану. Дозволи — списком назв, а не бітовою маскою. Key — ключ вбудованої ролі:
/// за ним клієнт показує назву мовою гравця; null — назву дав клан.
/// </summary>
public record ClanRoleResponse(
    Guid Id,
    string Name,
    string? Key,
    int Rank,
    List<ClanPermission> Permissions,
    bool IsLeaderRole,
    bool IsDefaultRole);

/// <summary>Учасник клану. RoleKey — як Key у ролі.</summary>
public record ClanMemberResponse(
    Guid PlayerId,
    string PlayerName,
    Guid RoleId,
    string RoleName,
    string? RoleKey,
    int Rank,
    double Power,
    DateTime JoinedAt,
    DateTime LastActiveAt);

/// <summary>Клан очима учасника: склад, ролі й власні дозволи.</summary>
public record MyClanResponse(
    Guid Id,
    string Name,
    string Tag,
    string Description,
    string JoinPolicy,
    int MemberCount,
    int Capacity,
    DateTime CreatedAt,
    Guid MyRoleId,
    List<ClanPermission> MyPermissions,
    List<ClanRoleResponse> Roles,
    List<ClanMemberResponse> Members);

/// <summary>Активний запит на допомогу. Subject — ключ технології «тір:вузол» для дослідження, інакше null.</summary>
public record ClanHelpItemResponse(
    Guid RequestId,
    Guid PlayerId,
    string PlayerName,
    string TargetType,
    Guid TargetId,
    int HelpCount,
    int MaxHelpers,
    bool AlreadyHelped,
    bool IsMine,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string? Subject);
