using System.Text;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Clients.Commands.AdminCreateClient;

/// <summary>
/// Command to create a new client account from the admin panel.
/// Supports both creating a new Identity user and linking to an existing one.
/// </summary>
/// <param name="CreateNewUser">True to create a new Identity user; false to link an existing one.</param>
/// <param name="ExistingUserId">The Identity user ID to link when <paramref name="CreateNewUser"/> is false.</param>
/// <param name="Email">Email address for the new user (required when <paramref name="CreateNewUser"/> is true).</param>
/// <param name="Password">Password for the new user (required when <paramref name="CreateNewUser"/> is true).</param>
/// <param name="FirstName">Client's first name.</param>
/// <param name="LastName">Client's last name.</param>
/// <param name="CompanyName">Optional company name.</param>
/// <param name="Phone">Optional phone number.</param>
/// <param name="Address">Billing address; the country is an ISO 3166-1 alpha-2 code.</param>
/// <param name="Preferences">
/// Language (e.g. "en", "hy", "ru"), ISO 4217 currency, payment method label, billing contact
/// reference and internal admin-only notes.
/// </param>
/// <param name="Status">The status the client starts in; null starts it <see cref="ClientStatus.Active"/>.</param>
/// <param name="Notifications">Email notification flags; every kind is on when left out.</param>
/// <param name="Settings">
/// Billing and account switches; when left out a new client gets reminders on, status updates on and
/// single sign-on allowed, with tax exemption, separate invoices, card-processing refusal and marketing opt-in off.
/// </param>
/// <param name="ClientGroupId">The client group to put the client in; null puts it in the default group, when one is set.</param>
public sealed record AdminCreateClientCommand(
    bool CreateNewUser,
    string? ExistingUserId,
    string? Email,
    string? Password,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    PostalAddress Address,
    ClientPreferencesDto Preferences,
    ClientStatus? Status,
    ContactNotifications? Notifications = null,
    ClientSettingsDto? Settings = null,
    int? ClientGroupId = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
