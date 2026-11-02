namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the staff alert about a gateway payment reported as paid with no reference to record it under.</summary>
/// <param name="Invoice">The invoice, read as <c>{{ invoice.id }}</c>.</param>
/// <param name="Client">The owning client, read as <c>{{ client.id }}</c>.</param>
/// <param name="Module">The payment plugin id, read as <c>{{ module }}</c>.</param>
/// <param name="Amount">The amount the gateway reports as taken, read as <c>{{ amount }}</c>; empty when it reports none.</param>
/// <param name="Currency">ISO 4217 code of the invoice, read as <c>{{ currency }}</c>.</param>
public sealed record GatewayPaymentWithoutReferenceAdminTemplateModel(
    InvoiceRefTemplateData Invoice,
    ClientRefTemplateData Client,
    string Module,
    decimal? Amount,
    string Currency) : EmailTemplateModel;
