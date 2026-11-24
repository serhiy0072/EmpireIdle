using EmpireIdle.Application.Common;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Wallets.ReadModels
{
    /// <param name="VipLevel">VIP-рівень від куплених за весь час gems (рішення 28.10.2026).</param>
    /// <param name="NextVipThreshold">Скільки куплених gems відкриє наступний рівень; null — рівень найвищий.</param>
    public record WalletView(int GemBalance, long PurchasedGems, int VipLevel, long? NextVipThreshold);
}
