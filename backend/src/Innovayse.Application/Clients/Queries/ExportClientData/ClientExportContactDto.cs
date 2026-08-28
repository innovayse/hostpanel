namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Domain.Clients;

/// <summary>One sub-account contact, as the <c>contacts</c> section of a client data export lists it.</summary>
/// <param name="Id">The contact's primary key.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact telephone number.</param>
/// <param name="Type">What the contact is for — billing, technical, and so on.</param>
public sealed record ClientExportContactDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    ContactType Type);
