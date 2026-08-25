namespace Innovayse.Providers.Inecobank;

/// <summary>Connection settings for the Inecobank payment gateway REST API.</summary>
/// <param name="BaseUrl">Gateway base URL, e.g. https://pg.inecoecom.am (no trailing path).</param>
/// <param name="UserName">Merchant API login.</param>
/// <param name="Password">Merchant API password.</param>
public sealed record InecobankClientOptions(string BaseUrl, string UserName, string Password);
