using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;

namespace EmpireIdle.Application.Wallets.Queries
{
    /// <summary>Запит на баланс гаманця гравця (акаунтний, не прив'язаний до села).</summary>
    public record GetWalletQuery(Guid PlayerId) : IRequest<WalletView>, IPlayerScopedRequest;

    public record WalletView(int GemBalance);

    public sealed class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, WalletView>
    {
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly ICurrentPlayer _currentPlayer;

        public GetWalletQueryHandler(IPlayerWalletRepository walletRepository, ICurrentPlayer currentPlayer)
        {
            _walletRepository = walletRepository;
            _currentPlayer = currentPlayer;
        }

        public async Task<WalletView> Handle(GetWalletQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

            var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("Wallet not found.");

            return new WalletView(wallet.GemBalance.Value);
        }
    }
}
