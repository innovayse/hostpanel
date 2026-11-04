using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>The registrar a domain is registered with and the identifiers that registrar holds for it.</summary>
/// <param name="Module">Name of the registrar module; null if not set.</param>
/// <param name="Reference">Reference ID from the registrar's system; null if not yet registered.</param>
/// <param name="EppCode">Authorization code for transfer; null if not available.</param>
public sealed record DomainRegistrarDto(string? Module, string? Reference, string? EppCode)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
