using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Contracts;
using EmpireIdle.Application.Heroes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>
    /// Герої гравця: ростер, призов за уламки, прокачка та еволюція тіру.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/heroes")]
    public class HeroesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public HeroesController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Ростер зі стелею рівня, ціною наступного рівня, уламками та пулом досвіду.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(HeroesOverview), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HeroesOverview>> GetOverview(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetHeroesOverviewQuery(playerId), cancellationToken));

        /// <summary>Перетворити універсальні осколки на осколки відкритого героя 1:1.</summary>
        [HttpPost("{playerId:guid}/shards/convert")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConvertShards(Guid playerId, [FromBody] ConvertUniversalShardsRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new ConvertUniversalShardsCommand(playerId, request.HeroKey, request.Count), cancellationToken);
            return NoContent();
        }

        /// <summary>Обміняти універсальні осколки на рідкість вищу (коли всі герої рідкості прокачані).</summary>
        [HttpPost("{playerId:guid}/shards/upgrade")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpgradeShards(Guid playerId, [FromBody] UpgradeUniversalShardsRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new UpgradeUniversalShardsCommand(playerId, request.From, request.Count), cancellationToken);
            return NoContent();
        }

        /// <summary>Поставити героя в навчальний табір: він отримує рівень опорної п'ятірки (GDD §6.1).</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/camp")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PlaceInCamp(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new PlaceHeroInCampCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Вийняти героя з табору: власний рівень повертається, слот перезаряджається.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/camp/leave")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveFromCamp(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new RemoveHeroFromCampCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Зняти перезарядку слота табору за gems.</summary>
        [HttpPost("{playerId:guid}/camp/slots/{slot:int}/skip-cooldown")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SkipCampCooldown(Guid playerId, int slot, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SkipCampCooldownCommand(playerId, slot), cancellationToken);
            return NoContent();
        }

        /// <summary>Докупити слот табору за gems.</summary>
        [HttpPost("{playerId:guid}/camp/slots")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BuyCampSlot(Guid playerId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new BuyCampSlotCommand(playerId), cancellationToken);
            return NoContent();
        }

        /// <summary>Заповнити наступну частинку зірки героя за його осколки.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/star")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AdvanceStar(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new AdvanceHeroStarCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Підняти вміння героя на рівень за книгу його ролі, рідкості й половини (GDD §6.1).</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/skills/{skillKey}/upgrade")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpgradeSkill(Guid playerId, Guid heroId, string skillKey, CancellationToken cancellationToken)
        {
            await _mediator.Send(new UpgradeHeroSkillCommand(playerId, heroId, skillKey), cancellationToken);
            return NoContent();
        }

        /// <summary>Призвати героя за накопичені уламки.</summary>
        [HttpPost("{playerId:guid}/summon")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Summon(Guid playerId, [FromBody] SummonHeroRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SummonHeroCommand(playerId, request.HeroKey), cancellationToken);
            return NoContent();
        }

        /// <summary>Підняти рівень героя одразу за досвід із пулу гравця (GDD §6.1).</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/level-up")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LevelUp(Guid playerId, Guid heroId, [FromQuery] int levels = 1,
            CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new LevelUpHeroCommand(playerId, heroId, levels), cancellationToken);
            return NoContent();
        }

        /// <summary>Скинути героя на перший рівень: досвід повертається в пул мінус штраф.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/reset-level")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetLevel(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new ResetHeroLevelCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Підняти тір героя за предмет еволюції.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/evolve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Evolve(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EvolveHeroTierCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Призначити героя лідером гарнізону, у якому він стоїть.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/appoint-leader")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AppointLeader(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new AppointGarrisonLeaderCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Вилікувати пораненого героя за ресурси.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/heal")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Heal(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new HealHeroCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Вдягнути спорядження на героя.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/equipment/{equipmentId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Equip(Guid playerId, Guid heroId, Guid equipmentId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new EquipHeroItemCommand(playerId, heroId, equipmentId), cancellationToken);
            return NoContent();
        }

        /// <summary>Зняти спорядження в інвентар.</summary>
        [HttpDelete("{playerId:guid}/equipment/{equipmentId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Unequip(Guid playerId, Guid equipmentId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new UnequipHeroItemCommand(playerId, equipmentId), cancellationToken);
            return NoContent();
        }
    }
}
