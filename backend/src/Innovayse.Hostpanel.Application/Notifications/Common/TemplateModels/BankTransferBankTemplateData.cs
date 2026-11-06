namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>
/// The operator's bank details as the bank transfer email reads them. A detail the operator left empty is an empty string,
/// so a template tests it with <c>{% if bank.iban != '' %}</c>.
/// </summary>
/// <param name="AccountName">The account holder, read as <c>account_name</c>.</param>
/// <param name="Iban">The IBAN, read as <c>iban</c>.</param>
/// <param name="Swift">The SWIFT/BIC code, read as <c>swift</c>.</param>
/// <param name="BankName">The bank's name, read as <c>bank_name</c>.</param>
/// <param name="Instructions">The operator's free text, read as <c>instructions</c>.</param>
public sealed record BankTransferBankTemplateData(string AccountName, string Iban, string Swift, string BankName, string Instructions);
