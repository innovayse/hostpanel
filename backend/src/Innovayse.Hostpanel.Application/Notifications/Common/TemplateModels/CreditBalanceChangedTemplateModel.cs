namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>credit-balance-changed</c> email: the client's credit balance moved and this is what it is now.</summary>
/// <param name="Credit">The change and the new balance, read as <c>{{ credit.* }}</c>.</param>
public sealed record CreditBalanceChangedTemplateModel(CreditBalanceTemplateData Credit) : EmailTemplateModel;
