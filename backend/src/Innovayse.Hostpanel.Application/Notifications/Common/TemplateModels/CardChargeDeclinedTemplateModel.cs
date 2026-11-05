namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>card-charge-declined</c> email.</summary>
/// <param name="Invoice">The invoice that stays unpaid, read as <c>{{ invoice.* }}</c>; its link is where the client pays another way.</param>
public sealed record CardChargeDeclinedTemplateModel(ChargedInvoiceTemplateData Invoice) : EmailTemplateModel;
