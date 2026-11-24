using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Banners.ReadModels
{
    /// <param name="RareSince">Скільки роллів гравець уже зробив без рідкісного в цій групі.</param>
    public record BannerView(
        string Key,
        string DisplayName,
        BannerKind Kind,
        string PityGroup,
        int PriceGems,
        int RarePity,
        int UniquePity,
        string? FeaturedKey,
        DateTimeOffset? EndsAt,
        int RareSince,
        int UniqueSince,
        bool FeaturedGuaranteed,
        IReadOnlyList<BannerDropView> Drops);

    /// <param name="Chance">Базовий шанс у відсотках, без урахування гарантій.</param>
    public record BannerDropView(string Key, string DisplayName, Rarity Rarity, BannerKind? Kind, double Chance);
}
