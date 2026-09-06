using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Клани: пошук, склад, ролі та кланова допомога.</summary>
    [ApiController]
    [Authorize]
    [Route("api/clans")]
    public class ClanController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ClanController(IMediator mediator) => _mediator = mediator;

        // ---------- Читання ----------

        /// <summary>
        /// Клани світу з пошуком за назвою або тегом. Сторінка до 50 рядків;
        /// склад не віддається — його бачать лише свої.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ClanListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Browse([FromQuery] string? search, [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(new BrowseClansQuery(search, page, pageSize), cancellationToken);

            var items = result.Items
                .Select(c => new ClanListItemResponse(c.Id, c.Name, c.Tag, c.Description,
                    c.JoinPolicy.ToString(), c.MemberCount, c.Capacity, c.IsFull))
                .ToList();

            return Ok(new ClanListResponse(items, result.Total, result.Page, result.PageSize));
        }

        /// <summary>Картка клану за id.</summary>
        [HttpGet("profile/{clanId:guid}")]
        [ProducesResponseType(typeof(ClanProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProfile(Guid clanId, CancellationToken cancellationToken)
        {
            var clan = await _mediator.Send(new GetClanProfileQuery(clanId), cancellationToken);

            return Ok(new ClanProfileResponse(clan.Id, clan.Name, clan.Tag, clan.Description,
                clan.JoinPolicy.ToString(), clan.MemberCount, clan.Capacity, clan.IsFull, clan.CreatedAt));
        }

        /// <summary>
        /// Клан гравця зі складом і дозволами. Гравець без клану отримує
        /// 200 і null: це нормальний стан екрана, а не помилка.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(MyClanResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyClan(Guid playerId, CancellationToken cancellationToken)
        {
            var clan = await _mediator.Send(new GetMyClanQuery(playerId), cancellationToken);

            if (clan is null)
                return Ok(null);

            var roles = clan.Roles
                .Select(r => new ClanRoleResponse(r.Id, r.Name, r.Rank,
                    PermissionNames(r.Permissions), r.IsLeaderRole, r.IsDefaultRole))
                .ToList();

            var members = clan.Members
                .Select(m => new ClanMemberResponse(m.PlayerId, m.PlayerName, m.RoleId, m.RoleName,
                    m.Rank, m.Power, m.JoinedAt, m.LastActiveAt))
                .ToList();

            return Ok(new MyClanResponse(clan.Id, clan.Name, clan.Tag, clan.Description,
                clan.JoinPolicy.ToString(), clan.MemberCount, clan.Capacity, clan.CreatedAt,
                clan.MyRoleId, PermissionNames(clan.MyPermissions), roles, members));
        }

        /// <summary>Активні запити на допомогу в клані гравця.</summary>
        [HttpGet("{playerId:guid}/help")]
        [ProducesResponseType(typeof(List<ClanHelpItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetHelp(Guid playerId, CancellationToken cancellationToken)
        {
            var items = await _mediator.Send(new GetClanHelpQuery(playerId), cancellationToken);

            var response = items
                .Select(h => new ClanHelpItemResponse(h.RequestId, h.PlayerId, h.PlayerName,
                    h.TargetType.ToString(), h.TargetId, h.HelpCount, h.MaxHelpers,
                    h.AlreadyHelped, h.IsMine, h.CreatedAt, h.ExpiresAt))
                .ToList();

            return Ok(response);
        }

        /// <summary>Заявки, що чекають рішення. Потрібен дозвіл Recruit.</summary>
        [HttpGet("{playerId:guid}/requests")]
        [ProducesResponseType(typeof(List<ClanApplicationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplications(Guid playerId, CancellationToken cancellationToken)
        {
            var items = await _mediator.Send(new GetClanApplicationsQuery(playerId), cancellationToken);

            var response = items
                .Select(a => new ClanApplicationResponse(a.RequestId, a.PlayerId, a.PlayerName,
                    a.Power, a.CreatedAt, a.ExpiresAt))
                .ToList();

            return Ok(response);
        }

        /// <summary>Запрошення, адресовані гравцеві.</summary>
        [HttpGet("{playerId:guid}/invites")]
        [ProducesResponseType(typeof(List<ClanInviteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetInvites(Guid playerId, CancellationToken cancellationToken)
        {
            var items = await _mediator.Send(new GetMyClanInvitesQuery(playerId), cancellationToken);

            var response = items
                .Select(i => new ClanInviteResponse(i.RequestId, i.ClanId, i.ClanName, i.ClanTag,
                    i.Description, i.MemberCount, i.Capacity, i.InvitedAt, i.ExpiresAt))
                .ToList();

            return Ok(response);
        }

        // ---------- Склад ----------

        /// <summary>Створити клан. Засновник стає лідером.</summary>
        [HttpPost("{playerId:guid}")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(Guid playerId, [FromBody] CreateClanRequest request,
            CancellationToken cancellationToken)
        {
            var clanId = await _mediator.Send(
                new CreateClanCommand(playerId, request.Name, request.Tag), cancellationToken);

            return Created((string?)null, clanId);
        }

        /// <summary>
        /// Вступити в клан. Відкритий приймає одразу, закритий створює заявку —
        /// що саме сталося, каже поле відповіді.
        /// </summary>
        [HttpPost("{playerId:guid}/join/{clanId:guid}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Join(Guid playerId, Guid clanId, CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(new JoinClanCommand(playerId, clanId), cancellationToken);

            return Ok(outcome.ToString());
        }

        /// <summary>Вийти з клану. Лідер спершу передає лідерство.</summary>
        [HttpPost("{playerId:guid}/leave")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Leave(Guid playerId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new LeaveClanCommand(playerId), cancellationToken);

            return NoContent();
        }

        /// <summary>Виключити учасника нижчого рангу.</summary>
        [HttpPost("{playerId:guid}/kick/{targetPlayerId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Kick(Guid playerId, Guid targetPlayerId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new KickMemberCommand(playerId, targetPlayerId), cancellationToken);

            return NoContent();
        }

        /// <summary>Призначити роль учаснику. Роль лідера передається окремо.</summary>
        [HttpPost("{playerId:guid}/members/{targetPlayerId:guid}/role")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AssignRole(Guid playerId, Guid targetPlayerId,
            [FromBody] AssignClanRoleRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new AssignRoleCommand(playerId, targetPlayerId, request.RoleId), cancellationToken);

            return NoContent();
        }

        /// <summary>Змінити опис і політику вступу. Потрібен дозвіл EditProfile.</summary>
        [HttpPut("{playerId:guid}/settings")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSettings(Guid playerId,
            [FromBody] UpdateClanSettingsRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new UpdateClanSettingsCommand(playerId, request.Description, request.JoinPolicy), cancellationToken);

            return NoContent();
        }

        /// <summary>Запросити гравця в клан. Потрібен дозвіл Recruit.</summary>
        [HttpPost("{playerId:guid}/invite/{targetPlayerId:guid}")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Invite(Guid playerId, Guid targetPlayerId,
            CancellationToken cancellationToken)
        {
            var requestId = await _mediator.Send(
                new InviteToClanCommand(playerId, targetPlayerId), cancellationToken);

            return Created((string?)null, requestId);
        }

        /// <summary>
        /// Рішення по заявці або запрошенню: заявку вирішує офіцер,
        /// запрошення — той, кого запросили.
        /// </summary>
        [HttpPost("{playerId:guid}/requests/{requestId:guid}/resolve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResolveRequest(Guid playerId, Guid requestId,
            [FromBody] ResolveClanRequestRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new ResolveClanRequestCommand(playerId, requestId, request.Approve), cancellationToken);

            return NoContent();
        }

        /// <summary>Зняти власну заявку або відкликати надіслане запрошення.</summary>
        [HttpPost("{playerId:guid}/requests/{requestId:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelRequest(Guid playerId, Guid requestId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new CancelClanRequestCommand(playerId, requestId), cancellationToken);

            return NoContent();
        }

        // ---------- Допомога ----------

        /// <summary>Попросити клан скоротити таймер будівлі або тренування.</summary>
        [HttpPost("{playerId:guid}/help")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestHelp(Guid playerId,
            [FromBody] RequestClanHelpRequest request, CancellationToken cancellationToken)
        {
            var requestId = await _mediator.Send(
                new RequestClanHelpCommand(playerId, request.TargetType, request.TargetId), cancellationToken);

            return Created((string?)null, requestId);
        }

        /// <summary>Допомогти по чужому запиту. Один гравець — одна допомога.</summary>
        [HttpPost("{playerId:guid}/help/{requestId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GiveHelp(Guid playerId, Guid requestId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new GiveClanHelpCommand(playerId, requestId), cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Відкликати всі свої підкріплення. Юніти йдуть маршем і будуть
        /// доступні лише після прибуття.
        /// </summary>
        /// <returns>Скільки маршів вирушило додому.</returns>
        [HttpPost("{playerId:guid}/reinforcements/recall")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecallReinforcements(Guid playerId, CancellationToken cancellationToken)
        {
            var sent = await _mediator.Send(new RecallReinforcementsCommand(playerId), cancellationToken);

            return Ok(sent);
        }

        /// <summary>
        /// Прапорці дозволів — списком назв: фронту простіше перевірити
        /// наявність у масиві, ніж робити бітову арифметику.
        /// </summary>
        private static List<string> PermissionNames(ClanPermission permissions)
            => Enum.GetValues<ClanPermission>()
                .Where(p => p != ClanPermission.None && permissions.HasFlag(p))
                .Select(p => p.ToString())
                .ToList();
    }
}
