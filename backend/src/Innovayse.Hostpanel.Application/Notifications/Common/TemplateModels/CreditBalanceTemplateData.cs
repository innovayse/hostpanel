namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>A move of the client's credit balance as the balance email reads it.</summary>
/// <param name="Change">The signed change (positive added, negative taken), read as <c>change</c>.</param>
/// <param name="Balance">The balance once the change was saved, read as <c>balance</c>.</param>
/// <param name="Currency">The ISO 4217 code the balance is held in, read as <c>currency</c>.</param>
/// <param name="Url">The absolute link to the credit page in the client area, read as <c>url</c>.</param>
public sealed record CreditBalanceTemplateData(decimal Change, decimal Balance, string Currency, string Url);
