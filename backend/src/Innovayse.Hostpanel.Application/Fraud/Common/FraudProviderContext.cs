namespace Innovayse.Hostpanel.Application.Fraud.Common;

/// <summary>What the external scoring service is asked about.</summary>
/// <param name="IpAddress">The caller's IP address.</param>
/// <param name="Email">The e-mail address, or <see langword="null"/> when none is known.</param>
/// <param name="BillingCountry">The billing country (ISO 3166-1 alpha-2), or <see langword="null"/> when none is on record.</param>
public sealed record FraudProviderContext(string IpAddress, string? Email, string? BillingCountry);
