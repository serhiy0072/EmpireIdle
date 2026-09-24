using EmpireIdle.Application.Catalog;
using EmpireIdle.Application.Catalog.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Довідник для інтерфейсу: назви, ранги, класи, стати.</summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/catalog")]
    public class CatalogController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CatalogController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Каталог гри мовою lang (без неї — мовою за замовчуванням). Незмінний
        /// у межах запуску, віддається з ETag; ETag різний для кожної мови.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(CatalogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        public async Task<ActionResult<CatalogResponse>> GetCatalog([FromQuery] string? lang, CancellationToken cancellationToken)
        {
            var catalog = await _mediator.Send(new GetCatalogQuery(lang), cancellationToken);
            var etag = $"\"{catalog.Version}\"";

            // no-cache не забороняє кеш, а вимагає перевірки: з ETag це 304 без тіла.
            // max-age тримав старий каталог годину після зміни конфіга
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.ETag = etag;

            // Клієнт уже має цю версію: тіло не потрібне
            if (Request.Headers[HeaderNames.IfNoneMatch].ToString() == etag)
                return StatusCode(StatusCodes.Status304NotModified);

            return Ok(catalog);
        }
    }
}
