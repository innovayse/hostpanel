namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.DeleteTaxRule;

/// <summary>Command to delete a tax rule. Issued invoices keep the rate they were taxed at.</summary>
/// <param name="Id">The identifier of the rule to delete.</param>
public sealed record DeleteTaxRuleCommand(int Id);
