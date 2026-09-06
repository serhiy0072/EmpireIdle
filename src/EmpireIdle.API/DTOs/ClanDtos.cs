using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

// ---------- Запити ----------

/// <summary>Створення клану. Тег зберігається у верхньому регістрі.</summary>
public record CreateClanRequest(string Name, string Tag);


/// <summary>Опис і політика вступу. Потрібен дозвіл EditProfile.</summary>
public record UpdateClanSettingsRequest(string Description, ClanJoinPolicy JoinPolicy);

/// <summary>Нова роль для учасника. Роль лідера так не передається.</summary>
public record AssignClanRoleRequest(Guid RoleId);

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

/// <summary>Роль клану. Дозволи — списком назв, а не бітовою маскою.</summary>
public record ClanRoleResponse(
    Guid Id,
    string Name,
    int Rank,
    List<string> Permissions,
    bool IsLeaderRole,
    bool IsDefaultRole);

/// <summary>Учасник клану.</summary>
public record ClanMemberResponse(
    Guid PlayerId,
    string PlayerName,
    Guid RoleId,
    string RoleName,
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
    List<string> MyPermissions,
    List<ClanRoleResponse> Roles,
    List<ClanMemberResponse> Members);

/// <summary>Активний запит на допомогу.</summary>
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
    DateTime ExpiresAt);

/// <summary>Заявка в черзі офіцера.</summary>
public record ClanApplicationResponse(
    Guid RequestId,
    Guid PlayerId,
    string PlayerName,
    double Power,
    DateTime CreatedAt,
    DateTime ExpiresAt);

/// <summary>Запрошення, адресоване гравцеві.</summary>
public record ClanInviteResponse(
    Guid RequestId,
    Guid ClanId,
    string ClanName,
    string ClanTag,
    string Description,
    int MemberCount,
    int Capacity,
    DateTime InvitedAt,
    DateTime ExpiresAt);
