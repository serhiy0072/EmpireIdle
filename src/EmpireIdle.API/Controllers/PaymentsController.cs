using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Payments.Commands;
using EmpireIdle.Application.Payments.Contracts;
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

            switch (result)
            {
                case { Outcome: PaymentWebhookOutcome.Paid, SessionId: { } paidSession }:
                    await _mediator.Send(new CompletePaymentCommand(paidSession), cancellationToken);
                    break;

                case { Outcome: PaymentWebhookOutcome.Failed, SessionId: { } failedSession }:
                    await _mediator.Send(new FailPaymentCommand(failedSession), cancellationToken);
                    break;
            }

            return Ok();
        }
        catch (InvalidWebhookSignatureException ex)
        {
            // Невалідний підпис — ретрай не допоможе, це або атака, або зламаний секрет
            _logger.LogWarning(ex, "Rejected Stripe webhook: signature validation failed.");
            return BadRequest();
        }
        catch (Exception ex)
        {
            // Внутрішній збій — 500 змусить Stripe повторити, коли ми полагодимось
            _logger.LogError(ex, "Failed to process Stripe webhook.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
