namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A payment gateway module a client can save a payment method with.</summary>
/// <param name="Module">Module id the "Add card" flow sends back.</param>
/// <param name="DisplayName">Label the client sees.</param>
public sealed record SavedMethodModuleDto(string Module, string DisplayName);
