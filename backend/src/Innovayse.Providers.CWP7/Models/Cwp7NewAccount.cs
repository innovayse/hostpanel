using System.Text;

namespace Innovayse.Providers.CWP7.Models;

/// <summary>The account a CWP7 server is asked to create.</summary>
/// <param name="Domain">Primary domain for the account.</param>
/// <param name="Username">Username to create.</param>
/// <param name="Password">Account password.</param>
/// <param name="Package">Hosting package name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="InodeLimit">Inode limit; 0 means unlimited.</param>
/// <param name="ProcessLimit">Most processes the account may run at once.</param>
/// <param name="OpenFileLimit">Most files the account may hold open at once.</param>
/// <param name="ServerIp">Server IP address the account is bound to.</param>
internal sealed record Cwp7NewAccount(
    string Domain,
    string Username,
    string Password,
    string Package,
    string Email,
    int InodeLimit,
    int ProcessLimit,
    int OpenFileLimit,
    string ServerIp)
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
        builder.Append(", InodeLimit = ").Append(InodeLimit);
        builder.Append(", ProcessLimit = ").Append(ProcessLimit);
        builder.Append(", OpenFileLimit = ").Append(OpenFileLimit);
        builder.Append(", ServerIp = ").Append(ServerIp);
        return true;
    }
}
