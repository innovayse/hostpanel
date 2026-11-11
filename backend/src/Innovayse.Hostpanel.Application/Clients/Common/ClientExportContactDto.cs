using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>One sub-account contact, as the <c>contacts</c> section of a client data export lists it.</summary>
/// <param name="Id">The contact's primary key.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="CompanyName">Company the contact acts for, when it is not a person.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact telephone number.</param>
/// <param name="Type">What the contact is for: billing, technical, and so on.</param>
/// <param name="Street">Address, first line.</param>
/// <param name="Address2">Address, second line.</param>
/// <param name="City">Address city.</param>
/// <param name="State">Address state or province.</param>
/// <param name="PostCode">Address postal code.</param>
/// <param name="Country">Address country.</param>
/// <param name="Notifications">Which kinds of email the contact receives a copy of.</param>
/// <param name="CreatedAt">When the contact was added.</param>
public sealed record ClientExportContactDto(
    int Id,
    string FirstName,
    string LastName,
    string? CompanyName,
    string Email,
    string? Phone,
    ContactType Type,
    string? Street,
    string? Address2,
    string? City,
    string? State,
    string? PostCode,
    string? Country,
    ClientExportNotificationsDto Notifications,
    DateTimeOffset CreatedAt);
