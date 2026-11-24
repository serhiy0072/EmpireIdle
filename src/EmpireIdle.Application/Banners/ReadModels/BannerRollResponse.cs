using EmpireIdle.Application.Common;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Banners.ReadModels
{
    /// <param name="Drops">Випади в порядку роллів.</param>
    /// <param name="GemBalance">Залишок після списання: клієнту не треба окремо перепитувати гаманець.</param>
    public record BannerRollResponse(
        string BannerKey,
        IReadOnlyList<BannerDropResult> Drops,
        int RareSince,
        int UniqueSince,
        int GemBalance);

    public record BannerDropResult(
        string DropKey,
        string DisplayName,
        Rarity Rarity,
        bool WasPity,
        bool LostFiftyFifty);
}
