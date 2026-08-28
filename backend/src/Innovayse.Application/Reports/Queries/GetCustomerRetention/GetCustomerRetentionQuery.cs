namespace Innovayse.Application.Reports.Queries.GetCustomerRetention;

/// <summary>Query for the Customer Retention report.</summary>
/// <param name="IncludeActive">
/// Whether services that are still running count towards the average. Excluding them measures
/// only completed lifetimes; including them counts time served so far.
/// </param>
public record GetCustomerRetentionQuery(bool IncludeActive = true);
