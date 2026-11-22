namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the staff alert about a gateway payment reported as no longer paid after the invoice was settled through it.</summary>
/// <param name="Invoice">The invoice, read as <c>{{ invoice.id }}</c>.</param>
/// <param name="Client">The owning client, read as <c>{{ client.id }}</c>.</param>
/// <param name="Module">The payment plugin id, read as <c>{{ module }}</c>.</param>
public sealed record GatewayPaymentReversedAdminTemplateModel(
    InvoiceRefTemplateData Invoice,
    ClientRefTemplateData Client,
    string Module) : EmailTemplateModel;
