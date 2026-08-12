using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Payments.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers;

/// <summary>Купівля gems.</summary>
[ApiController]
[Authorize]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentProvider _paymentProvider;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IMediator mediator, IPaymentProvider paymentProvider, ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _paymentProvider = paymentProvider;
        _logger = logger;
    }

    /// <summary>Створює сесію оплати й повертає посилання на Stripe Checkout.</summary>
    [HttpPost("{playerId:guid}/checkout/{packKey}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCheckout(Guid playerId, string packKey, CancellationToken cancellationToken)
    {
        var url = await _mediator.Send(new CreateCheckoutSessionCommand(playerId, packKey), cancellationToken);
        return Ok(new { checkoutUrl = url });
    }

    /// <summary>
    /// Вебхук Stripe. Анонімний — автентичність доводить підпис, не токен.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            var result = _paymentProvider.ParseWebhook(payload, signature);

            if (result.IsPaymentCompleted && result.SessionId is not null)
                await _mediator.Send(new CompletePaymentCommand(result.SessionId), cancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            // 400 змусить Stripe повторити спробу — краще, ніж мовчки втратити оплату
            _logger.LogError(ex, "Failed to process Stripe webhook.");
            return BadRequest();
        }
    }

}
