using System.Text;

namespace Innovayse.Providers.CWP.Models;

/// <summary>Where and how the CWP client reaches one CWP server.</summary>
/// <param name="BaseUrl">CWP server base URL including port, e.g. "https://cwp.example.com:2304".</param>
/// <param name="ApiKey">CWP API key for authentication.</param>
internal sealed record CwpConnection(string BaseUrl, string ApiKey)
{
    /// <summary>Prints the record without its secret members, so the generated <c>ToString</c> never reveals them.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns>Always <see langword="true"/>: members are written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("BaseUrl = ").Append(BaseUrl);
        builder.Append(", ApiKey = ***");
        return true;
    }
}
