namespace Innovayse.Application.Billing.Options;

/// <summary>
/// The origins a hosted payment gateway may return a payer to.
/// </summary>
/// <remarks>
/// This binds no section of its own. The list it holds is the API layer's
/// <c>Cors:AllowedOrigins</c>, which the web edge owns and this layer only borrows: naming a
/// section constant here would claim ownership of one that belongs to something else. The
/// composition root is the single place the two are connected.
/// </remarks>
public sealed class GatewayReturnUrlOptions
{
    /// <summary>
    /// Absolute origins (scheme and authority, e.g. <c>https://host.example.com</c>, no path) a
    /// payer may be redirected back to after paying. Composed at the composition root from the
    /// API's <c>Cors:AllowedOrigins</c>. Empty means no return URL is accepted at all, which
    /// refuses every gateway payment rather than allowing an unchecked redirect.
    /// </summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];
}
