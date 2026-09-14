namespace Innovayse.Application.Billing.Currencies.Queries.ListCurrencies;

/// <summary>Lists the configured currencies for the admin currencies page.</summary>
/// <param name="EnabledOnly">When <see langword="true"/>, rows the operator switched off are left out.</param>
public record ListCurrenciesQuery(bool EnabledOnly = false);
