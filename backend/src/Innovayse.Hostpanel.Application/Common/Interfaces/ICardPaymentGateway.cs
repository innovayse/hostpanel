using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Abstraction over card gateway payment operations. Implemented in Infrastructure. Every
/// operation takes one typed instruction and answers with one typed result, so no caller passes a
/// positional list of primitives or a free-form dictionary.
/// </summary>
public interface ICardPaymentGateway
{
    /// <summary>
    /// Whether the deployment carries a card gateway secret key at all. <c>false</c> is a deployment
    /// that takes no card payments through the card gateway; every other member then answers
    /// <see cref="Billing.Common.Enums.CardGatewayFailure.NotConfigured"/> without calling anything.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Creates a card gateway payment intent for an order's invoice.
    /// </summary>
    /// <param name="instruction">The amount, currency and the ids the intent is tied to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The intent, carrying the client secret the frontend uses to confirm the payment, or the
    /// failure kind when the gateway refused or could not be reached; a gateway failure never throws.
    /// </returns>
    Task<CardPaymentIntentResult> CreatePaymentIntentAsync(CardPaymentIntentInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Reads back a payment intent: whether it succeeded, and the amount, currency and ids it was opened for.
    /// </summary>
    /// <param name="instruction">The payment intent to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The intent's status as the gateway records it.</returns>
    Task<CardPaymentIntentStatus> VerifyPaymentIntentAsync(CardPaymentVerifyInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Charges a payment method saved against a card gateway customer, off session, for the full amount.
    /// </summary>
    /// <remarks>
    /// The only way money is taken from a saved method. A charge the gateway does not settle at once
    /// (declined, or needing the cardholder to authenticate) is an unsuccessful result, never a success.
    /// </remarks>
    /// <param name="instruction">The customer, the saved method, the amount, the currency and the invoice paid.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the money was taken, and the transaction ID (charge ID or intent ID) when it was.</returns>
    Task<CardChargeResult> ChargeSavedPaymentMethodAsync(CardChargeInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Finds the charge a saved-method charge with this idempotency key already took, without
    /// charging anything: a key the gateway never charged under answers that nothing was found.
    /// </summary>
    /// <param name="instruction">The customer, the invoice and the idempotency key of the payment attempt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The settled charge of that attempt, <see cref="CardChargeLookupResult.None"/>, or the failure
    /// kind when the gateway refused or could not be reached; a gateway failure never throws.
    /// </returns>
    Task<CardChargeLookupResult> FindChargeAsync(CardChargeLookupInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Opens a customer for a client at the card gateway, the object cards are saved against.
    /// </summary>
    /// <remarks>
    /// The customer carries the client's id on it and the request an idempotency key, so two requests
    /// made at the same moment for one client answer with one customer.
    /// </remarks>
    /// <param name="instruction">The client, its name and e-mail, and the idempotency key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The customer's id, or the failure kind when the gateway refused or could not be reached; a gateway failure never throws.</returns>
    Task<CardCustomerResult> CreateCustomerAsync(CardCreateCustomerInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Finds the customer the gateway holds for a client by the client id kept on it, without creating one.
    /// </summary>
    /// <remarks>
    /// Recovers a customer that was opened but never stored (a crash between the two steps), so it is
    /// adopted instead of duplicated.
    /// </remarks>
    /// <param name="instruction">The client whose customer is looked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The customer's id, <see cref="CardCustomerResult.None"/>, or the failure kind; a gateway failure never throws.</returns>
    Task<CardCustomerResult> FindCustomerAsync(CardFindCustomerInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Deletes a customer the gateway opened, used only to drop one that lost a race to another
    /// customer already stored for the same client.
    /// </summary>
    /// <param name="instruction">The customer to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the failure kind; a gateway failure never throws.</returns>
    Task<CardGatewayOperationResult> DeleteCustomerAsync(CardCustomerInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Opens a setup intent so the client can save a card without paying anything.
    /// </summary>
    /// <param name="instruction">The customer the card is saved against, the client, and the idempotency key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The setup intent with the secret the browser confirms it with, or the failure kind; a gateway failure never throws.</returns>
    Task<CardSetupIntentResult> CreateSetupIntentAsync(CardSetupIntentInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Reads back a setup intent: whether the card was saved, against which customer and as which payment method.
    /// </summary>
    /// <param name="instruction">The setup intent to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The setup intent's outcome as the gateway records it; a gateway failure never throws.</returns>
    Task<CardSetupIntentStatus> VerifySetupIntentAsync(CardSetupIntentVerifyInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Refunds an amount of a previously completed payment.
    /// </summary>
    /// <param name="instruction">The charge ID (<c>ch_xxx</c>) or PaymentIntent ID (<c>pi_xxx</c>) to refund, and the amount and currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refund, or an unsuccessful result when the gateway refuses it.</returns>
    Task<CardRefundResult> RefundAsync(CardRefundInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Lists the cards and bank accounts saved against a card gateway customer.
    /// </summary>
    /// <param name="instruction">The card gateway customer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The customer's saved payment methods, with the default one flagged, or the failure kind when
    /// the gateway refused or could not be reached; a gateway failure never throws.
    /// </returns>
    Task<CardPaymentMethodsResult> ListPaymentMethodsAsync(CardCustomerInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Sets a customer's default payment method for future off-session charges.
    /// </summary>
    /// <param name="instruction">The customer and the payment method to make the default.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the failure kind when the gateway refused or could not be reached; a gateway failure never throws.</returns>
    Task<CardGatewayOperationResult> SetDefaultPaymentMethodAsync(CardDefaultMethodInstruction instruction, CancellationToken ct);

    /// <summary>
    /// Detaches a payment method from whichever customer it belongs to, permanently
    /// removing it from the card gateway.
    /// </summary>
    /// <param name="instruction">The payment method to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the failure kind when the gateway refused or could not be reached; a gateway failure never throws.</returns>
    Task<CardGatewayOperationResult> DetachPaymentMethodAsync(CardDetachInstruction instruction, CancellationToken ct);
}
