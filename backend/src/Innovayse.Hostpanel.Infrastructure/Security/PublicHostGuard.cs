using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Infrastructure.Security.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Infrastructure.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Security;

/// <summary>
/// Decides whether a client-supplied host name may be dialled by this server, and hands back the
/// addresses that were vetted so the caller connects to exactly those.
/// </summary>
/// <remarks>
/// <para>
/// A domain a client typed is not trusted text: probed naively it makes this server a way to map its
/// own network and reach the metadata endpoint. The guard refuses IP literals, single-label names and
/// names that are not plain host names before any lookup, then resolves the name itself and refuses it
/// when <b>any</b> address is not public (<see cref="OutboundAddressPolicy"/>, the one table of ranges
/// every guarded outbound connection is checked against; a client's domain is never on a private network).
/// </para>
/// <para>
/// The caller must dial the returned addresses and not the name again: a second resolution could answer
/// differently (DNS rebinding) and undo the check.
/// </para>
/// </remarks>
internal static partial class PublicHostGuard
{
    /// <summary>The longest host name DNS allows.</summary>
    private const int MaxNameLength = 253;

    /// <summary>How long one resolution may take before the name counts as unresolved.</summary>
    private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(3);

    /// <summary>A DNS label: letters, digits and inner hyphens, at most 63 characters.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex("^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex LabelPattern();

    /// <summary>Whether the text is a plain multi-label host name and not an IP literal.</summary>
    /// <param name="host">The text to check.</param>
    /// <returns>
    /// <see langword="true"/> for a name of two or more letter-digit-hyphen labels whose last label is
    /// not numeric (no real top-level domain is, and an all-digit last label is how short IPv4 spellings
    /// such as <c>127.1</c> look).
    /// </returns>
    public static bool IsPublicHostName(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > MaxNameLength || IPAddress.TryParse(host, out _))
        {
            return false;
        }

        var labels = host.Split('.');
        return labels.Length >= 2
            && labels.All(label => LabelPattern().IsMatch(label))
            && !labels[^1].All(char.IsAsciiDigit);
    }

    /// <summary>Checks the name and resolves it, refusing it when any address is internal.</summary>
    /// <param name="host">The client-supplied host name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verdict and, for a public name, the addresses to dial.</returns>
    public static async Task<PublicHostResolution> ResolveAsync(string? host, CancellationToken ct)
    {
        if (!IsPublicHostName(host))
        {
            return PublicHostResolution.Refused;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ResolveTimeout);

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host!, timeout.Token);
        }
        catch (SocketException)
        {
            return PublicHostResolution.Unresolved;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The resolver was merely slow; the caller's own cancellation still propagates.
            return PublicHostResolution.Unresolved;
        }

        if (addresses.Length == 0)
        {
            return PublicHostResolution.Unresolved;
        }

        return addresses.Any(a => OutboundAddressPolicy.Classify(a) != OutboundAddressClass.Public)
            ? PublicHostResolution.Refused
            : new PublicHostResolution(PublicHostVerdict.Public, addresses);
    }
}
