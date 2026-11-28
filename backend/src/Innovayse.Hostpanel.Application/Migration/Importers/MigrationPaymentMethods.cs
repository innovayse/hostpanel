using System.Diagnostics.CodeAnalysis;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Maps the payment method a source invoice or transaction names to the name this panel records the
/// same method under, so an imported bank transfer is stored like one confirmed here.
/// </summary>
public static class MigrationPaymentMethods
{
    /// <summary>Translates a source payment method name to the local one.</summary>
    /// <param name="source">The source's payment method name, or <see langword="null"/> when it sent none.</param>
    /// <returns>
    /// <see cref="PaymentMethodNames.BankTransfer"/> for a source bank transfer; any other name as received,
    /// since a gateway a plugin brings keeps its own name.
    /// </returns>
    [return: NotNullIfNotNull(nameof(source))]
    public static string? ToPaymentMethodName(string? source)
    {
        if (string.Equals(source, SourcePaymentMethods.BankTransfer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, SourcePaymentMethods.ExportDefault, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentMethodNames.BankTransfer;
        }

        return source;
    }
}
