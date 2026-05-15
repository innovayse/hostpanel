namespace Innovayse.Application.Clients.DTOs;

using Innovayse.Domain.Clients;

/// <summary>DTO for an additional client contact.</summary>
/// <param name="Id">Contact primary key.</param>
/// <param name="FirstName">Contact first name.</param>
/// <param name="LastName">Contact last name.</param>
/// <param name="CompanyName">Optional company name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact phone number (null if not set).</param>
/// <param name="Type">Contact type classification.</param>
/// <param name="Street">Street address (null if not set).</param>
/// <param name="Address2">Second address line (null if not set).</param>
/// <param name="City">City (null if not set).</param>
/// <param name="State">State or region (null if not set).</param>
/// <param name="PostCode">Postal code (null if not set).</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code (null if not set).</param>
/// <param name="NotifyGeneral">General email notifications enabled.</param>
/// <param name="NotifyInvoice">Invoice email notifications enabled.</param>
/// <param name="NotifySupport">Support email notifications enabled.</param>
/// <param name="NotifyProduct">Product email notifications enabled.</param>
/// <param name="NotifyDomain">Domain email notifications enabled.</param>
/// <param name="NotifyAffiliate">Affiliate email notifications enabled.</param>
public record ContactDto(
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
    bool NotifyGeneral,
    bool NotifyInvoice,
    bool NotifySupport,
    bool NotifyProduct,
    bool NotifyDomain,
    bool NotifyAffiliate);
