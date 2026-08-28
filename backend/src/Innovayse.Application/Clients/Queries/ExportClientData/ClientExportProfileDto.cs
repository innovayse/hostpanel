namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Domain.Clients;

/// <summary>The <c>profileData</c> section of a client data export: who the account belongs to.</summary>
/// <param name="Id">The client's primary key.</param>
/// <param name="FirstName">Given name on the account.</param>
/// <param name="LastName">Family name on the account.</param>
/// <param name="Email">Sign-in address, read from the identity store rather than the client row.</param>
/// <param name="CompanyName">Company the account bills as, when it is not a person.</param>
/// <param name="Phone">Contact telephone number.</param>
/// <param name="Street">Billing address, first line.</param>
/// <param name="City">Billing address city.</param>
/// <param name="State">Billing address state or province.</param>
/// <param name="PostCode">Billing address postal code.</param>
/// <param name="Country">Billing address country.</param>
/// <param name="Status">Account status — Active, Inactive, Suspended or Closed.</param>
/// <param name="CreatedAt">When the client record was opened.</param>
public sealed record ClientExportProfileDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? CompanyName,
    string? Phone,
    string? Street,
    string? City,
    string? State,
    string? PostCode,
    string? Country,
    ClientStatus Status,
    DateTimeOffset CreatedAt);
