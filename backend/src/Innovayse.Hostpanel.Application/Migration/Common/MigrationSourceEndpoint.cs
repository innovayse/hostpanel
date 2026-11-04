using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Migration.Common;

/// <summary>Where a migration job's legacy source listens and the key it accepts; every call to the source carries both.</summary>
/// <param name="Url">The address the source answers on, as configured on the job.</param>
/// <param name="AccessKey">The job's shared key; the source refuses a call that carries any other.</param>
public sealed record MigrationSourceEndpoint(string Url, string AccessKey)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
