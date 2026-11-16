using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>
/// The few facts about a client record that other modules show or act on (a name on a list, the
/// address a notification goes to, the currency an order is priced in), so they never read the
/// Clients tables themselves.
/// </summary>
/// <param name="Id">The client record's id.</param>
/// <param name="UserSubject">The identity subject of the person who owns the record.</param>
/// <param name="FirstName">The client's first name.</param>
/// <param name="LastName">The client's last name.</param>
/// <param name="CompanyName">The client's company, when one is set.</param>
/// <param name="Address">The client's postal address, for a document that is addressed to them.</param>
/// <param name="Email">
/// The address the owner signs in with; <see langword="null"/> when it was not asked for or the
/// identity store no longer knows the owner.
/// </param>
/// <param name="Language">
/// The owner's chosen language (ISO 639-1), resolved with <paramref name="Email"/>; <see langword="null"/>
/// when it was not asked for or the owner chose none.
/// </param>
/// <param name="Currency">The client's billing currency code, when one is set.</param>
/// <param name="Status">The client's account status.</param>
/// <param name="NotifyInvoice">Whether the client wants invoice emails, renewal reminders included.</param>
/// <param name="Settings">The account switches staff set on the client: reminders, tax, invoice grouping, card processing and single sign-on.</param>
/// <param name="Vat">The last completed check of the client's VAT number, which the reverse charge on their invoices depends on.</param>
/// <param name="CreditBalance">
/// The client's credit balance, in the client's billing currency (the payer currency its invoices are
/// raised in); the balance has no currency of its own.
/// </param>
/// <param name="CreatedAt">When the client record was created.</param>
public sealed record ClientBriefDto(
    int Id,
    string UserSubject,
    string FirstName,
    string LastName,
    string? CompanyName,
    ClientAddressDto Address,
    string? Email,
    string? Language,
    string? Currency,
    ClientStatus Status,
    bool NotifyInvoice,
    ClientSettingsDto Settings,
    ClientVatDto Vat,
    decimal CreditBalance,
    DateTimeOffset CreatedAt)
{
    /// <summary>The first and last name joined, trimmed when either is blank.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
