using System.Text;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>The part of the UK tax authority's token answer that the check reads.</summary>
/// <param name="AccessToken">The bearer token the lookup is sent with; the authority spells the member <c>access_token</c>.</param>
internal sealed record HmrcTokenWire([property: JsonPropertyName("access_token")] string? AccessToken)
{
    /// <summary>Prints the record without its token, so no log line or exception message can carry it.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
