namespace Innovayse.Application.Services.Common;

/// <summary>
/// Enforces that a client service belongs to the calling client.
/// </summary>
/// <remarks>
/// <para>
/// Shaped like <c>IDomainOwnership</c>, <c>ITicketOwnership</c> and <c>IInvoiceOwnership</c>:
/// subject to client to resource, resolving the caller itself through
/// <c>ICurrentRequestContext</c> rather than being told whom to check, and refusing rather than
/// answering a <see langword="bool"/> — so a handler cannot ask the question and then forget to
/// act on the answer.
/// </para>
/// <para>
/// <b>Services had no such rule before this type.</b> <c>GetMyServiceInvoicesQuery</c> was the
/// first client-facing service use case to carry one; the five routes that took a service id
/// straight off <c>MyServicesController</c> and checked nothing now do too. They dispatch
/// <c>GetMyServiceCPanelSsoUrlQuery</c>, <c>CancelMyServiceCommand</c>,
/// <c>SetupMyServiceCommand</c>, <c>ChangeMyServicePasswordCommand</c> and
/// <c>GetMyServiceCancellationStatusQuery</c>, each of which settles ownership here and then
/// delegates to the unscoped use case the admin routes keep using.
/// </para>
/// <para>
/// Every message that must pass through this rule carries
/// <see cref="ICallerScopedServiceMessage"/>, so the requirement is written on the message type
/// and a test can check that a newly added route did not skip it.
/// </para>
/// </remarks>
public interface IServiceOwnership
{
    /// <summary>
    /// Verifies that the service belongs to the client the current credential names, and refuses
    /// the request when it does not.
    /// </summary>
    /// <param name="serviceId">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when, and only when, the caller owns the service.</returns>
    /// <exception cref="MyServiceNotFoundException">
    /// Thrown when the service belongs to somebody else, when no such service exists, and when
    /// the caller has no client record. The three answer alike on purpose: telling them apart
    /// would let the route be used to enumerate service ids.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task RequireOwnedByCallerAsync(int serviceId, CancellationToken ct);

    /// <summary>
    /// Resolves the client the current credential names, refusing when there is not one.
    /// </summary>
    /// <remarks>
    /// A handler that has verified ownership of a service almost always needs the owning client
    /// id straight afterwards — to read that client's invoices, for instance. Asking this type
    /// for it rather than resolving the caller a second time keeps "who is calling" answered in
    /// one place, and keeps the answer the same one ownership was decided against.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The calling client's primary key.</returns>
    /// <exception cref="Innovayse.Application.Clients.Common.ClientProfileNotFoundException">
    /// Thrown when the caller is authenticated but has no client record.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task<int> RequireCallerClientIdAsync(CancellationToken ct);
}
