using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Створює власну роль клану (GDD §7.1). Права, ранги й ліміт кількості перевіряє агрегат.
    /// Повертає Id нової ролі.
    /// </summary>
    public record CreateClanRoleCommand(Guid PlayerId, string Name, int Rank, ClanPermission Permissions)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Змінює назву, ранг і дозволи ролі. Роль лідера незмінна.</summary>
    public record UpdateClanRoleCommand(Guid PlayerId, Guid RoleId, string Name, int Rank, ClanPermission Permissions)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Видаляє роль; її носії переходять на роль за замовчуванням.</summary>
    public record DeleteClanRoleCommand(Guid PlayerId, Guid RoleId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CreateClanRoleCommandHandler : IRequestHandler<CreateClanRoleCommand, Guid>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CreateClanRoleCommandHandler> _logger;

        public CreateClanRoleCommandHandler(IClanRepository clanRepository, IUnitOfWork unitOfWork, GameCatalog catalog,
            TimeProvider timeProvider, ILogger<CreateClanRoleCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(CreateClanRoleCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var clan = await ClanRoleCommands.ClanOfAsync(_clanRepository, request.PlayerId, cancellationToken);

            var roleId = clan.CreateRole(request.PlayerId, request.Name.Trim(), request.Rank, request.Permissions,
                _catalog.Config.Clan.MaxRoles, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {ActorId} created role {RoleId} in clan {ClanId}", request.PlayerId, roleId, clan.Id);

            return roleId;
        }
    }

    public sealed class UpdateClanRoleCommandHandler : IRequestHandler<UpdateClanRoleCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UpdateClanRoleCommandHandler> _logger;

        public UpdateClanRoleCommandHandler(IClanRepository clanRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider,
            ILogger<UpdateClanRoleCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(UpdateClanRoleCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var clan = await ClanRoleCommands.ClanOfAsync(_clanRepository, request.PlayerId, cancellationToken);

            clan.UpdateRole(request.PlayerId, request.RoleId, request.Name.Trim(), request.Rank, request.Permissions, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {ActorId} updated role {RoleId} in clan {ClanId}", request.PlayerId, request.RoleId, clan.Id);
        }
    }

    public sealed class DeleteClanRoleCommandHandler : IRequestHandler<DeleteClanRoleCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DeleteClanRoleCommandHandler> _logger;

        public DeleteClanRoleCommandHandler(IClanRepository clanRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider,
            ILogger<DeleteClanRoleCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DeleteClanRoleCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var clan = await ClanRoleCommands.ClanOfAsync(_clanRepository, request.PlayerId, cancellationToken);

            clan.DeleteRole(request.PlayerId, request.RoleId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {ActorId} deleted role {RoleId} in clan {ClanId}", request.PlayerId, request.RoleId, clan.Id);
        }
    }

    /// <summary>Спільне для трьох команд ролей: клан виконавця або відмова «ви не в клані».</summary>
    internal static class ClanRoleCommands
    {
        public static async Task<Clan> ClanOfAsync(IClanRepository clans, Guid playerId, CancellationToken cancellationToken)
            => await clans.GetByMemberAsync(playerId, cancellationToken)
               ?? throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");
    }
}
