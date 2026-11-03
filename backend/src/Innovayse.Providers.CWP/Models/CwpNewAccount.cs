using System.Text;

namespace Innovayse.Providers.CWP.Models;

/// <summary>The account a CWP server is asked to create.</summary>
/// <param name="Domain">Primary domain for the account.</param>
/// <param name="Username">Username to create.</param>
/// <param name="Password">Account password.</param>
/// <param name="Package">Hosting package name.</param>
/// <param name="Email">Contact email address.</param>
public sealed record CwpNewAccount(
    string Domain,
    string Username,
    string Password,
    string Package,
    string Email)
{
    /// <summary>Prints the record without its secret members, so the generated <c>ToString</c> never reveals them.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns>Always <see langword="true"/>: members are written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Domain = ").Append(Domain);
        builder.Append(", Username = ").Append(Username);
        builder.Append(", Password = ***");
        builder.Append(", Package = ").Append(Package);
        builder.Append(", Email = ").Append(Email);
        return true;
    }
}
