using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.EvaluateFraudRisk;

/// <summary>
/// Scores a registration or an order that is about to happen. Asked by the Orders and Auth modules from inside their own use
/// cases, never bound from a request: the caller's IP address is read in the handler, not stated here.
/// </summary>
/// <param name="Kind">What is being checked.</param>
/// <param name="Email">The e-mail address of the person, when known.</param>
/// <param name="BillingCountry">The billing country on record (ISO 3166-1 alpha-2), when there is one.</param>
public sealed record EvaluateFraudRiskQuery(FraudCheckKind Kind, string? Email, string? BillingCountry);
