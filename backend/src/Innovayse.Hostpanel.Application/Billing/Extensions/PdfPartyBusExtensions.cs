using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Reads the two parties every PDF document of the panel prints, the seller from the portal settings and the client from the
/// Clients module, so an invoice and a credit note are addressed the same way.
/// </summary>
public static class PdfPartyBusExtensions
{
    /// <summary>The portal settings the seller block and logo are read from.</summary>
    private static readonly List<string> SellerKeys =
        [PortalSettingKeys.SiteName, PortalSettingKeys.ContactAddress, PortalSettingKeys.LegalTaxId, PortalSettingKeys.Logo];

    /// <summary>Reads the seller from the portal settings.</summary>
    /// <param name="bus">The message bus the settings are asked for over.</param>
    /// <param name="taxIdPrefix">The word printed before the tax identifier, such as <c>Tax ID</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The seller party and the logo setting; a seller with no site name prints the default site name.</returns>
    public static async Task<InvoicePdfSeller> ReadPdfSellerAsync(this IMessageBus bus, string taxIdPrefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var rows = await bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingValuesQuery(SellerKeys), ct);
        var settings = (rows.Value ?? [])
            .Where(s => s.Value is not null)
            .ToDictionary(s => s.Key, s => s.Value!.Trim(), StringComparer.Ordinal);
        string Setting(string key) => settings.GetValueOrDefault(key) ?? string.Empty;

        var sellerName = Setting(PortalSettingKeys.SiteName);
        var taxId = Setting(PortalSettingKeys.LegalTaxId);
        var sellerLines = Setting(PortalSettingKeys.ContactAddress)
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (taxId.Length > 0)
        {
            sellerLines.Add($"{taxIdPrefix}: {taxId}");
        }

        var logo = Setting(PortalSettingKeys.Logo);
        return new InvoicePdfSeller(
            new InvoicePdfParty(sellerName.Length > 0 ? sellerName : EmailBranding.DefaultSiteName, sellerLines),
            logo.Length > 0 ? logo : null);
    }

    /// <summary>Addresses a document to a client: the name in bold, then the company and the postal address.</summary>
    /// <param name="client">The client the document is for.</param>
    /// <returns>The party printed under the bill-to heading, empty address lines removed.</returns>
    public static InvoicePdfParty ToPdfParty(this ClientBriefDto client)
    {
        ArgumentNullException.ThrowIfNull(client);

        string?[] candidates =
        [
            client.CompanyName,
            client.Address.Street,
            client.Address.Address2,
            $"{client.Address.PostCode} {client.Address.City}".Trim(),
            client.Address.State,
            client.Address.Country,
        ];
        return new InvoicePdfParty(
            client.FullName,
            candidates.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList());
    }
}
