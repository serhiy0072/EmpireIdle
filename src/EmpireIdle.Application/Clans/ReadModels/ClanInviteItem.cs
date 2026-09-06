namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>Запрошення, адресоване гравцеві, з карткою клану.</summary>
    public record ClanInviteItem(
        Guid RequestId,
        Guid ClanId,
        string ClanName,
        string ClanTag,
        string Description,
        int MemberCount,
        int Capacity,
        DateTime InvitedAt,
        DateTime ExpiresAt);
}
