using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("stripe/webhook")]
[AllowAnonymous]
[EnableRateLimiting(RateLimiterPolicyNames.StripeWebhook)]
public sealed class StripeWebhookController(
    IPaymentGateway paymentGateway,
    ICheckoutAttemptRepository checkoutAttempts,
    IOrderFulfillmentService fulfillment,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        var evt = paymentGateway.ConstructEvent(payload, signature);
        if (evt is null) return BadRequest();

        try
        {
            switch (evt.Type)
            {
                case "payment_intent.succeeded" when evt.PaymentIntentId is not null:
                    {
                        var attemptId = await checkoutAttempts.FindIdByPaymentIntentIdAsync(evt.PaymentIntentId, cancellationToken);
                        if (attemptId is not null)
                            await fulfillment.FulfillAsync(attemptId.Value, cancellationToken);
                        else
                            logger.LogWarning("Stripe payment_intent.succeeded for unknown intent {PaymentIntentId}", evt.PaymentIntentId);
                        break;
                    }
                case "payment_intent.canceled" when evt.PaymentIntentId is not null:
                    await checkoutAttempts.MarkFailedByPaymentIntentIdAsync(evt.PaymentIntentId, cancellationToken);
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            // Permanent: this event can never be fulfilled. Acknowledge it so Stripe stops retrying.
            logger.LogError(ex, "Stripe webhook {EventType} for intent {PaymentIntentId} could not be processed",
                evt.Type, evt.PaymentIntentId);
            return Ok();
        }
        catch (Exception ex)
        {
            throw new StripeWebhookProcessingException(
                $"Stripe webhook {evt.Type} for intent {evt.PaymentIntentId} failed transiently", ex);
        }

        return Ok();
    }
}

public sealed class StripeWebhookProcessingException : Exception
{
    public StripeWebhookProcessingException(string message, Exception inner) : base(message, inner) { }
}
