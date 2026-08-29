namespace Innovayse.Application.Migration.Commands.ImportBatch;

/// <summary>A single client record.</summary>
/// <param name="Email">Client email address.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="Company">Company name, if any.</param>
/// <param name="Phone">Phone number, if any.</param>
/// <param name="Address">Street address, if any.</param>
/// <param name="City">City, if any.</param>
/// <param name="State">State or region, if any.</param>
/// <param name="PostCode">Postal code, if any.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code, if any.</param>
/// <param name="Status">Client status string.</param>
public sealed record MigrationClientRecord(
    string Email, string FirstName, string LastName,
    string? Company, string? Phone,
    string? Address, string? City, string? State, string? PostCode, string? Country,
    string Status);
