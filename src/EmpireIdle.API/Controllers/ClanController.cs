using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Application.Clans.ReadModels;
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
        [ProducesResponseType(typeof(ClanListPage), StatusCodes.Status200OK)]
        public async Task<IActionResult> Browse([FromQuery] string? search, [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            return Ok(await _mediator.Send(new BrowseClansQuery(search, page, pageSize), cancellationToken));
        }

        /// <summary>Картка клану за id.</summary>
        [HttpGet("profile/{clanId:guid}")]
        [ProducesResponseType(typeof(ClanProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProfile(Guid clanId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetClanProfileQuery(clanId), cancellationToken));
        }

        /// <summary>
        /// Клан гравця зі складом і дозволами. Гравець без клану отримує
        /// 200 і null: це нормальний стан екрана, а не помилка.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(MyClan), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyClan(Guid playerId, CancellationToken cancellationToken)
        {
            // null — гравець поза кланом: Ok(null) віддає 204, клієнт зводить його до порожнього стану
            return Ok(await _mediator.Send(new GetMyClanQuery(playerId), cancellationToken));
        }

        /// <summary>Активні запити на допомогу в клані гравця.</summary>
        [HttpGet("{playerId:guid}/help")]
        [ProducesResponseType(typeof(List<ClanHelpItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetHelp(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetClanHelpQuery(playerId), cancellationToken));
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

        /// <summary>Передати лідерство учаснику. Лише лідер; сам він стає на другу за рангом роль.</summary>
        [HttpPost("{playerId:guid}/members/{targetPlayerId:guid}/leadership")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TransferLeadership(Guid playerId, Guid targetPlayerId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new TransferLeadershipCommand(playerId, targetPlayerId), cancellationToken);

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

        /// <summary>Створити власну роль клану. Потрібен дозвіл ManageRoles.</summary>
        [HttpPost("{playerId:guid}/roles")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateRole(Guid playerId, [FromBody] ClanRoleRequest request,
            CancellationToken cancellationToken)
        {
            var roleId = await _mediator.Send(
                new CreateClanRoleCommand(playerId, request.Name, request.Rank, Combine(request.Permissions)), cancellationToken);

            return Created((string?)null, roleId);
        }

        /// <summary>Змінити назву, ранг і дозволи ролі. Роль лідера незмінна.</summary>
        [HttpPut("{playerId:guid}/roles/{roleId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateRole(Guid playerId, Guid roleId, [FromBody] ClanRoleRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new UpdateClanRoleCommand(playerId, roleId, request.Name, request.Rank, Combine(request.Permissions)), cancellationToken);

            return NoContent();
        }

        /// <summary>Видалити роль: її носії переходять на роль за замовчуванням.</summary>
        [HttpDelete("{playerId:guid}/roles/{roleId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRole(Guid playerId, Guid roleId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteClanRoleCommand(playerId, roleId), cancellationToken);

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

        /// <summary>Назад із списку назв у прапорці — для запитів, що задають дозволи ролі.</summary>
        private static ClanPermission Combine(IEnumerable<ClanPermission>? permissions)
            => (permissions ?? []).Aggregate(ClanPermission.None, (all, permission) => all | permission);
    }
}
