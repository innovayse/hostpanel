using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>A card payment intent opened for an order's invoice.</summary>
/// <param name="ClientSecret">The secret the payer's browser confirms the card payment with.</param>
public sealed record PaymentIntentDto(string ClientSecret)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
