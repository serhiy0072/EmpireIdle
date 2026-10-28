using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Wallets.Queries
{
    /// <summary>Запит на баланс гаманця гравця (акаунтний, не прив'язаний до села).</summary>
    public record GetWalletQuery(Guid PlayerId) : IRequest<WalletView>, IPlayerScopedRequest;

    /// <param name="VipLevel">VIP-рівень від куплених за весь час gems (рішення 28.10.2026).</param>
    /// <param name="NextVipThreshold">Скільки куплених gems відкриє наступний рівень; null — рівень найвищий.</param>
    public record WalletView(int GemBalance, long PurchasedGems, int VipLevel, long? NextVipThreshold);

    public sealed class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, WalletView>
    {
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly VipProgression _vip;

        public GetWalletQueryHandler(IPlayerWalletRepository walletRepository, ICurrentPlayer currentPlayer, VipProgression vip)
        {
            _walletRepository = walletRepository;
            _currentPlayer = currentPlayer;
            _vip = vip;
        }

        public async Task<WalletView> Handle(GetWalletQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

            var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("Wallet not found.");

            var vipLevel = _vip.Level(wallet.PurchasedGems);

            return new WalletView(wallet.GemBalance.Value, wallet.PurchasedGems, vipLevel, _vip.NextThreshold(vipLevel));
        }
    }
}
