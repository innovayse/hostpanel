namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the staff alert about a gateway payment the invoice could not record, kept on the client's balance or held.</summary>
/// <param name="Invoice">The invoice, read as <c>{{ invoice.id }}</c>.</param>
/// <param name="Client">The owning client, read as <c>{{ client.id }}</c>.</param>
/// <param name="Module">The payment plugin id, read as <c>{{ module }}</c>.</param>
/// <param name="Reference">The gateway's reference of the payment, read as <c>{{ reference }}</c>.</param>
/// <param name="Amount">The amount the invoice could not record, read as <c>{{ amount }}</c>.</param>
/// <param name="Currency">ISO 4217 code of the amount, read as <c>{{ currency }}</c>.</param>
/// <param name="Credited">Whether the amount went to the client's balance (otherwise it is held for an operator), read as <c>{{ credited }}</c>.</param>
public sealed record GatewayPaymentCreditedAdminTemplateModel(
    InvoiceRefTemplateData Invoice,
    ClientRefTemplateData Client,
    string Module,
    string Reference,
    decimal Amount,
    string Currency,
    bool Credited) : EmailTemplateModel;
