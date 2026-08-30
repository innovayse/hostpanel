namespace Innovayse.Application.Services.Common;

/// <summary>
/// Thrown when a client-facing service use case is asked for a service the caller may not have.
/// <para>
/// One type deliberately covers three situations: the service belongs to another client, no
/// service with that id exists at all, and the caller has no client record. Service ids are
/// sequential integers, so answering them apart -- 403 here, 404 there -- would turn
/// <c>/api/me/services/{id}/…</c> into a way of finding out which ids exist and which of them
/// are somebody else's. All three answer 404 carrying <see cref="Code"/>.
/// </para>
/// <para>
/// It is deliberately <b>not</b> the existing <c>ClientServiceNotFound</c> resource string, which
/// reads "ClientService {0} not found." and is thrown as a plain
/// <see cref="InvalidOperationException"/> by the admin-side and provisioning handlers. That
/// sentence names the id back at the caller, which is exactly the confirmation a probe is
/// looking for. This type is shaped like <c>DomainNotFoundException</c>,
/// <c>TicketNotFoundException</c> and <c>InvoiceNotFoundException</c> instead, so the whole
/// client-facing surface refuses alike.
/// </para>
/// </summary>
/// <param name="serviceId">The service id that was asked for. For the server-side log only.</param>
public sealed class MyServiceNotFoundException(int serviceId) : Exception(PublicMessage)
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. The frontend branches
    /// on this string, so it is part of the wire contract and must not be reworded.
    /// </summary>
    public const string Code = "MY_SERVICE_NOT_FOUND";

    /// <summary>
    /// Key of the sentence in <c>Innovayse.Application/Resources/ValidationMessages.resx</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="PublicMessage"/> is still the English text and is still what
    /// <see cref="System.Exception.Message"/> carries, so a log line and a test read the same
    /// sentence they always did. What the caller is shown is looked up under this key instead,
    /// because the portal ships in en/ru/hy.
    /// </remarks>
    public const string MessageKey = "MyServiceNotFound";

    /// <summary>
    /// The sentence the caller is shown. It deliberately says nothing about whether the service
    /// exists or who it belongs to -- that is the fact this type exists to withhold.
    /// </summary>
    public const string PublicMessage = "No such service.";

    /// <summary>
    /// The service id that was asked for. Logged server-side; never written to the response
    /// body, because echoing it back tells the caller their probe was understood.
    /// </summary>
    public int ServiceId { get; } = serviceId;
}
