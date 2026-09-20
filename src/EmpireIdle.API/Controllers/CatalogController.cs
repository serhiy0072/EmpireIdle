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
        /// <summary>Година в кеші браузера: конфіг міняється з деплоєм, а не в рантаймі.</summary>
        private const int CacheSeconds = 3600;

        private readonly IMediator _mediator;

        public CatalogController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Каталог гри. Незмінний у межах запуску, віддається з ETag.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(CatalogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        public async Task<ActionResult<CatalogResponse>> GetCatalog(CancellationToken cancellationToken)
        {
            var catalog = await _mediator.Send(new GetCatalogQuery(), cancellationToken);
            var etag = $"\"{catalog.Version}\"";

            Response.Headers.CacheControl = $"public, max-age={CacheSeconds}";
            Response.Headers.ETag = etag;

            // Клієнт уже має цю версію: тіло не потрібне
            if (Request.Headers[HeaderNames.IfNoneMatch].ToString() == etag)
                return StatusCode(StatusCodes.Status304NotModified);

            return Ok(catalog);
        }
    }
}
