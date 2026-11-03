using System.Text;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Migration;

/// <summary>The body of a source call that fetches one page of a dataset.</summary>
/// <param name="AccessKey">The job's shared key the source checks before answering.</param>
/// <param name="Action">The dataset to read, in the source's own vocabulary.</param>
/// <param name="Page">The page to fetch, counting from 1.</param>
/// <param name="PerPage">The most records the page may hold.</param>
/// <param name="AfterId">
/// The source row id the page starts after. A plugin that pages by keyset reads it and answers
/// with the id the next page starts after; an older plugin ignores it and answers by
/// <paramref name="Page"/>.
/// </param>
internal sealed record MigrationSourcePageWire([property: JsonPropertyName("key")] string AccessKey, string Action, int Page, int PerPage, long AfterId)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
