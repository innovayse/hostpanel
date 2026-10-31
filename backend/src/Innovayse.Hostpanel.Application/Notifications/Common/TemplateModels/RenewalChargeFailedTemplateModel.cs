namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the e-mails that tell a client the automatic charge of a renewal invoice did not go through.</summary>
/// <param name="Invoice">The invoice that is still unpaid, read as <c>{{ invoice.* }}</c>; its url is the pay page.</param>
public sealed record RenewalChargeFailedTemplateModel(RenewalInvoiceTemplateData Invoice) : EmailTemplateModel;
