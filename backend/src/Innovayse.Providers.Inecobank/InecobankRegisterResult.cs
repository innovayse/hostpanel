namespace Innovayse.Providers.Inecobank;

/// <summary>Successful register.do response.</summary>
/// <param name="OrderId">Gateway-side order id.</param>
/// <param name="FormUrl">Hosted payment page URL.</param>
public sealed record InecobankRegisterResult(string OrderId, string FormUrl);
