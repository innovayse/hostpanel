namespace Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;

/// <summary>
/// Raises the renewal invoice(s) one client is due. Its own message so each client runs in its own
/// Wolverine scope, a fresh <c>DbContext</c>, and one client's failure cannot leak tracked
/// entities into the next client's save.
/// </summary>
/// <param name="ClientId">The client; for a staff request, set from the route.</param>
/// <param name="Manual">
/// <see langword="true"/> when staff asked for the run (the activity log records it); the daily sweep leaves it
/// <see langword="false"/>. It says why the run happens, not who runs it: the caller is read from the token.
/// </param>
public sealed record RaiseClientRenewalInvoicesCommand(int ClientId, bool Manual = false);
