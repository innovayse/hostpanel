namespace Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;

/// <summary>The payment method names the source system sends for a built-in method. They are the source's own spellings, so they cannot be renamed.</summary>
public static class SourcePaymentMethods
{
    /// <summary>The source's module name <c>banktransfer</c> for a manual bank transfer.</summary>
    public const string BankTransfer = "banktransfer";

    /// <summary>The name <c>bank_transfer</c> the source exporter sends when a record names no gateway.</summary>
    public const string ExportDefault = "bank_transfer";
}
