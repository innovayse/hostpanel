using System.Text;

namespace Innovayse.Providers.CWP7.Models;

/// <summary>Where and how the CWP7 client reaches one CWP7 server.</summary>
/// <param name="BaseUrl">CWP7 API base URL including port, e.g. "https://cwp.example.com:2304".</param>
/// <param name="ApiKey">CWP7 API key for authentication.</param>
/// <param name="UserPanelUrl">
/// Base URL of the panel's user-facing sign-in page, e.g. "https://cwp.example.com:2083". It is a
/// different port from <paramref name="BaseUrl"/> and is only shown to the account owner.
/// </param>
internal sealed record Cwp7Connection(string BaseUrl, string ApiKey, string UserPanelUrl)
{
    /// <summary>Prints the record without its secret members, so the generated <c>ToString</c> never reveals them.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns>Always <see langword="true"/>: members are written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("BaseUrl = ").Append(BaseUrl);
        builder.Append(", ApiKey = ***");
        builder.Append(", UserPanelUrl = ").Append(UserPanelUrl);
        return true;
    }
}
