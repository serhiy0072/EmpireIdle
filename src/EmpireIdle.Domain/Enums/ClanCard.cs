using EmpireIdle.Domain.Enums;

/// <summary>
/// Картка клану для списків: без складу й ролей, лічильник учасників
/// підзапитом. Тягнути агрегат заради назви й кількості — це сто кланів
/// по двісті учасників у пам'яті на один екран.
/// </summary>
public record ClanCard(
    Guid Id,
    string Name,
    string Tag,
    string Description,
    ClanJoinPolicy JoinPolicy,
    int MemberCount,
    DateTime CreatedAt);
