using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One name and value of the query the payer's browser came back from the gateway with.</summary>
/// <param name="Name">The parameter name, compared ordinally.</param>
/// <param name="Value">
/// The parameter value, untrusted input from the payer's browser. It may be a one-time payment token, so the printed form of
/// the record masks it.
/// </param>
public sealed record PaymentReturnParameterDto(string Name, [property: SecretMember] string Value)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
