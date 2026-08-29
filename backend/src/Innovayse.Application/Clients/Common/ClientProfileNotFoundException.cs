namespace Innovayse.Application.Clients.Common;

/// <summary>
/// Thrown when the authenticated user has no row in the <c>clients</c> table, so there is no
/// customer record for a "my …" query or command to act on.
/// <para>
/// This is not a fault. Staff identities — the platform superadmin above all — are real,
/// authenticated users who were simply never onboarded as customers, and every client-portal
/// endpoint they touch answers this. It is a distinct type rather than an
/// <see cref="InvalidOperationException"/> precisely so the API layer can answer 404 with a
/// code the browser can branch on, instead of a 400 carrying an English sentence the frontend
/// would have to string-match.
/// </para>
/// <para>
/// The message sent to the caller is the constant <see cref="PublicMessage"/> and carries no
/// identifier. The user id lives on <see cref="UserId"/> for the server-side log only: it is an
/// internal identity key, and a browser that is shown one has been told something about the
/// platform's internals that it has no use for. That leak — a raw UUID rendered in a red alert
/// on the client dashboard — is the defect this type exists to close.
/// </para>
/// </summary>
/// <param name="userId">Identity subject of the user that has no client record.</param>
public sealed class ClientProfileNotFoundException(string userId) : Exception(PublicMessage)
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. The frontend
    /// branches on this string, so it is part of the wire contract and must not be reworded.
    /// </summary>
    public const string Code = "CLIENT_PROFILE_NOT_FOUND";

    /// <summary>
    /// Key of the sentence in <c>Innovayse.Application/Resources/ValidationMessages.resx</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="PublicMessage"/> is still the English text and is still what
    /// <see cref="System.Exception.Message"/> carries, so a log line and a test read the same
    /// sentence they always did. What the caller is shown is looked up under this key instead,
    /// because the portal ships in en/ru/hy and a customer reading Russian or Armenian was
    /// previously served this English constant for every failure the frontend had no entry for.
    /// </remarks>
    public const string MessageKey = "ClientProfileNotFound";

    /// <summary>
    /// The sentence the caller is shown. Deliberately free of identifiers, and deliberately
    /// worded as a state rather than a failure — nothing went wrong when a staff account has
    /// no customer record.
    /// </summary>
    public const string PublicMessage =
        "This account has no client profile, so there is nothing to show in the client area.";

    /// <summary>
    /// Identity subject of the user that has no client record. Logged server-side; never
    /// written to the response body.
    /// </summary>
    public string UserId { get; } = userId;
}
