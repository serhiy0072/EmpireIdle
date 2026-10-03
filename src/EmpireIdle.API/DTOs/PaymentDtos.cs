namespace EmpireIdle.API.DTOs;

/// <summary>Сесія оплати створена: клієнт переходить за посиланням на Stripe Checkout.</summary>
public record CheckoutResponse(string CheckoutUrl);
