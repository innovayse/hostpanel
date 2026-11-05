using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one rule that says where a payer may be handed back to after a gateway step: the origin of the address must
/// be one of the panel's configured origins. Shared by every use case that sends a payer to a gateway.
/// </summary>
public static class ReturnUrlPolicy
{
    /// <summary>
    /// Whether <paramref name="returnUrl"/>'s origin is one of the panel's configured CORS origins, so a payer can
    /// never be handed off to an attacker-controlled domain.
    /// </summary>
    /// <param name="returnUrl">The absolute URL the payer will be redirected back to.</param>
    /// <param name="options">The configured origins.</param>
    /// <returns><see langword="true"/> when the URL is absolute and its origin is allowed.</returns>
    public static bool IsAllowed(string? returnUrl, CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var origin = $"{uri.Scheme}://{uri.Authority}";
        return options.Origins.Any(o =>
            string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }
}
