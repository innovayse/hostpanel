using System.Text;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>What the people seeder needs to create one demo customer.</summary>
/// <param name="Email">Sign-in email.</param>
/// <param name="Password">Sign-in password.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Company">Company name.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Street">Street address.</param>
/// <param name="City">City.</param>
/// <param name="State">State or region; null where the country has none.</param>
/// <param name="PostCode">Postal code.</param>
/// <param name="Country">ISO 3166 alpha-2 country code.</param>
/// <param name="Currency">ISO 4217 billing currency.</param>
/// <param name="Status">The account state the customer ends up in.</param>
/// <param name="Credit">Credit balance the customer starts with; zero for none.</param>
internal sealed record DemoClientProfile(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Company,
    string Phone,
    string Street,
    string City,
    string? State,
    string PostCode,
    string Country,
    string Currency,
    ClientStatus Status = ClientStatus.Active,
    decimal Credit = 0m)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
