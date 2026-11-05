namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>card-charge-action-required</c> email.</summary>
/// <param name="Invoice">The invoice being paid, read as <c>{{ invoice.* }}</c>.</param>
/// <param name="ActionUrl">Where the payer authenticates, read as <c>{{ action_url }}</c>; the invoice link when the plugin gave none.</param>
public sealed record CardChargeActionRequiredTemplateModel(ChargedInvoiceTemplateData Invoice, string ActionUrl) : EmailTemplateModel;
