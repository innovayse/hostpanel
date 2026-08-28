namespace Innovayse.Application.Clients.Interfaces;

using Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>
/// Reads the client-scoped data the client data export needs and no existing repository exposes.
/// <para>
/// Most of the export comes from the ordinary repositories — clients, domains, services, invoices,
/// transactions, quotes, tickets and activity logs all already answer "for this client". Two reads
/// do not fit them:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>IBillableItemRepository</c> only offers the invoiced, uninvoiced and recurring slices, while
/// the export wants every item on the account whatever its state.
/// </description></item>
/// <item><description>
/// <c>IEmailLogRepository</c> resolves a client's address from the local identity table. The export
/// resolves it through <c>IIdentityProvider</c> instead, which is the account's real address when
/// the product is pointed at an external SSO, so it asks for the log by address rather than by
/// client id.
/// </description></item>
/// </list>
/// </summary>
public interface IClientExportRepository
{
    /// <summary>
    /// Returns every billable item belonging to a client — invoiced and uninvoiced alike — in the
    /// shape the export prints them.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's billable items; empty when the client has none.</returns>
    Task<IReadOnlyList<ClientExportBillableItemDto>> ListBillableItemsAsync(
        int clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns every logged email sent to one address, in the shape the export prints them.
    /// </summary>
    /// <param name="recipient">The address the mail was sent to; matched exactly.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching log entries; empty when nothing was ever sent to that address.</returns>
    Task<IReadOnlyList<ClientExportEmailDto>> ListEmailsForRecipientAsync(
        string recipient,
        CancellationToken ct = default);
}
