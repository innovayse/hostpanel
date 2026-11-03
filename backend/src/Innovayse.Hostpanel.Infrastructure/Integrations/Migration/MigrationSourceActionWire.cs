using System.Text;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Migration;

/// <summary>The body of a source call that takes no arguments: a ping or a totals request.</summary>
/// <param name="AccessKey">The job's shared key the source checks before answering.</param>
/// <param name="Action">The operation to run, in the source's own vocabulary.</param>
internal sealed record MigrationSourceActionWire([property: JsonPropertyName("key")] string AccessKey, string Action)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
