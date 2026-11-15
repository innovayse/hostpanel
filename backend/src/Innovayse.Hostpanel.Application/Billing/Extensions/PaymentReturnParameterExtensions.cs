using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one place the host turns the query the payer came back with into the parameters a payment plugin receives. The limits
/// of <see cref="PaymentReturnLimits"/> are held before this, by the validators of the complete routes.
/// </summary>
public static class PaymentReturnParameterExtensions
{
    /// <summary>
    /// Builds the read only set a plugin's <see cref="IPaymentReturnPlugin.CompleteAsync"/> is called with. Names are compared
    /// ordinally and the first value of a name wins. A name without text or a missing value is not a parameter and is left
    /// out.
    /// </summary>
    /// <param name="returned">The names and values of the return request, in the order they arrived.</param>
    /// <returns>The parameters, empty when the return carried none.</returns>
    public static IReadOnlyDictionary<string, string> ToPluginParameters(this IReadOnlyList<PaymentReturnParameterDto> returned)
    {
        ArgumentNullException.ThrowIfNull(returned);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in returned)
        {
            if (string.IsNullOrEmpty(parameter?.Name) || parameter.Value is null)
            {
                continue;
            }

            parameters.TryAdd(parameter.Name, parameter.Value);
        }

        return parameters;
    }
}
