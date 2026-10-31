namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Where a client's agreement to automatic card charges came from, kept with the agreement as proof.</summary>
/// <param name="IpAddress">The address the request came from, or <see langword="null"/> when unknown.</param>
/// <param name="UserAgent">The browser the request came from, or <see langword="null"/> when unknown.</param>
public sealed record CardConsentOrigin(string? IpAddress, string? UserAgent);
