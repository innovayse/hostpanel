namespace Innovayse.API.Clients.Requests;

using Innovayse.Domain.Clients;

/// <summary>HTTP request body for POST /api/clients/{id}/contacts.</summary>
/// <param name="FirstName">Contact first name.</param>
/// <param name="LastName">Contact last name.</param>
/// <param name="CompanyName">Optional company name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Optional phone number.</param>
/// <param name="Type">Contact type.</param>
/// <param name="Street">Optional street address.</param>
/// <param name="Address2">Optional second address line.</param>
/// <param name="City">Optional city.</param>
/// <param name="State">Optional state or region.</param>
/// <param name="PostCode">Optional postal code.</param>
/// <param name="Country">Optional ISO 3166-1 alpha-2 country code.</param>
/// <param name="NotifyGeneral">General email notifications (default true).</param>
/// <param name="NotifyInvoice">Invoice email notifications (default true).</param>
/// <param name="NotifySupport">Support email notifications (default true).</param>
/// <param name="NotifyProduct">Product email notifications (default true).</param>
/// <param name="NotifyDomain">Domain email notifications (default true).</param>
/// <param name="NotifyAffiliate">Affiliate email notifications (default true).</param>
public record AddContactRequest(
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
    bool NotifyGeneral = true,
    bool NotifyInvoice = true,
    bool NotifySupport = true,
    bool NotifyProduct = true,
    bool NotifyDomain = true,
    bool NotifyAffiliate = true);
