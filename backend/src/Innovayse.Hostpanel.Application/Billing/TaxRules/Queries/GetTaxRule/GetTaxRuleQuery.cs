namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.GetTaxRule;

/// <summary>Query to retrieve a single tax rule by id.</summary>
/// <param name="Id">The identifier of the rule to retrieve.</param>
public sealed record GetTaxRuleQuery(int Id);
