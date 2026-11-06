using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Queries.GetIntegration;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Reads the bank transfer integration the admin saved. The settings belong to the Admin module, so they are asked for
/// through its <see cref="GetIntegrationQuery"/>; every use case that shows the details or checks the switch goes through
/// here, so the field keys and the blank rule are written once.
/// </summary>
public static class BankTransferSettingsExtensions
{
    /// <summary>Reads the switch and the bank details of the bank transfer integration.</summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings; switched off with no details when nothing is stored.</returns>
    public static async Task<BankTransferSettings> LoadBankTransferSettingsAsync(this IMessageBus bus, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var outcome = await bus.InvokeAsync<Result<IntegrationDetailDto>>(
            new GetIntegrationQuery(BuiltInPaymentModules.BankTransferIntegrationSlug), ct);
        if (outcome.Value is not { } integration)
        {
            return new BankTransferSettings(false, null, null, null, null, null);
        }

        return new BankTransferSettings(
            integration.IsEnabled,
            FieldOf(integration, BankTransferFields.AccountName),
            FieldOf(integration, BankTransferFields.Iban),
            FieldOf(integration, BankTransferFields.Swift),
            FieldOf(integration, BankTransferFields.BankName),
            FieldOf(integration, BankTransferFields.Instructions));
    }

    /// <summary>Reads one stored field, treating a blank value as not set.</summary>
    /// <param name="integration">The integration's stored state.</param>
    /// <param name="field">The field key.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when it is missing or blank.</returns>
    private static string? FieldOf(IntegrationDetailDto integration, string field)
    {
        if (!integration.Config.TryGetValue(field, out var stored) || string.IsNullOrWhiteSpace(stored.Value))
        {
            return null;
        }

        return stored.Value.Trim();
    }
}
