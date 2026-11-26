using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.API.Auth;

/// <summary>
/// Reads the issuer a bearer token names in its payload, without ever throwing: the one place that
/// question is asked about a token nobody has validated yet.
/// </summary>
/// <remarks>
/// The scheme selector runs before any validation, so it sees whatever a caller put in the header. The
/// token libraries only offer readers that throw on a broken token (and their <c>CanReadToken</c> checks the
/// shape, not the encoding), so this reads the payload with the framework's status-returning base64url decoder (its <c>Try</c> variant throws on bad characters)
/// and answers false for anything that is not a compact token carrying a string <c>iss</c>. The answer only
/// routes the request to a scheme; that scheme still validates the token completely.
/// </remarks>
public static class BearerTokenIssuer
{
    /// <summary>The character that separates the header, the payload and the signature of a compact token.</summary>
    private const char SegmentSeparator = '.';

    /// <summary>The number of segments of a signed compact token: header, payload and signature.</summary>
    private const int SignedSegmentCount = 3;

    /// <summary>The position of the payload among the segments of a compact token.</summary>
    private const int PayloadSegment = 1;

    /// <summary>Reads the issuer the token claims.</summary>
    /// <param name="token">The text after the <c>Bearer </c> prefix, as the caller sent it.</param>
    /// <param name="issuer">The token's <c>iss</c> claim, when the token is a readable compact token that has one.</param>
    /// <returns>True when the token is readable and names an issuer; false for anything else.</returns>
    public static bool TryRead(string token, [NotNullWhen(true)] out string? issuer)
    {
        issuer = null;
        var segments = token.Split(SegmentSeparator);
        if (segments.Length != SignedSegmentCount)
        {
            return false;
        }

        var encoded = segments[PayloadSegment];
        var payload = new byte[Base64Url.GetMaxDecodedLength(encoded.Length)];
        if (Base64Url.DecodeFromChars(encoded, payload, out _, out var written) != OperationStatus.Done)
        {
            return false;
        }

        // No JSON reader of the framework answers with a value instead of an exception, so the one
        // exception it documents for malformed text is the way a payload that is no JSON is refused.
        try
        {
            using var document = JsonDocument.Parse(payload.AsMemory(0, written));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(JwtClaims.Issuer, out var claim)
                || claim.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            issuer = claim.GetString();
            return issuer is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
