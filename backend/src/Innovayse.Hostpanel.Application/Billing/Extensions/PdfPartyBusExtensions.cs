using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
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
    /// <summary>The contrast against white an accent colour needs to be read as text: WCAG AA for normal text.</summary>
    private const double AccentContrast = 4.5;

    /// <summary>The shade step the accent falls back to when the brand colour is too light to read on white.</summary>
    private const int AccentFallbackStep = 700;

    /// <summary>Reads the seller from the portal settings, the address in the language of the document.</summary>
    /// <param name="bus">The message bus the settings are asked for over.</param>
    /// <param name="labels">The words of the document, which name the language the localized address is read in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The seller party, the logo setting and the accent colour. The name is the company name, else the site name, else the
    /// default site name; the lines are the address, tax id, registration number, e-mail and phone that are set.
    /// </returns>
    public static async Task<InvoicePdfSeller> ReadPdfSellerAsync(this IMessageBus bus, DocumentPdfLabels labels, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(labels);

        var localizedAddressKey = PortalSettingKeys.ForLanguage(PortalSettingKeys.ContactAddress, labels.Language);
        List<string> keys =
        [
            PortalSettingKeys.SiteName,
            PortalSettingKeys.LegalCompanyName,
            PortalSettingKeys.ContactAddress,
            localizedAddressKey,
            PortalSettingKeys.LegalTaxId,
            PortalSettingKeys.LegalRegistrationNumber,
            PortalSettingKeys.ContactEmail,
            PortalSettingKeys.ContactPhone,
            PortalSettingKeys.Logo,
            PortalSettingKeys.BrandPrimary,
        ];
        var rows = await bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingValuesQuery(keys), ct);
        var settings = (rows.Value ?? [])
            .Where(s => s.Value is not null)
            .ToDictionary(s => s.Key, s => s.Value!.Trim(), StringComparer.Ordinal);
        string Setting(string key) => settings.GetValueOrDefault(key) ?? string.Empty;

        // A language with no address of its own prints the default one, as the storefront does.
        var address = Setting(localizedAddressKey).Length > 0 ? Setting(localizedAddressKey) : Setting(PortalSettingKeys.ContactAddress);
        var lines = address
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        AddLabelled(lines, labels.TaxId, Setting(PortalSettingKeys.LegalTaxId));
        AddLabelled(lines, labels.RegistrationNumber, Setting(PortalSettingKeys.LegalRegistrationNumber));
        AddLabelled(lines, null, Setting(PortalSettingKeys.ContactEmail));
        AddLabelled(lines, null, Setting(PortalSettingKeys.ContactPhone));

        var name = new[] { Setting(PortalSettingKeys.LegalCompanyName), Setting(PortalSettingKeys.SiteName) }
            .FirstOrDefault(n => n.Length > 0) ?? EmailBranding.DefaultSiteName;
        var logo = Setting(PortalSettingKeys.Logo);
        return new InvoicePdfSeller(new InvoicePdfParty(name, lines), logo.Length > 0 ? logo : null, AccentOf(Setting(PortalSettingKeys.BrandPrimary)));
    }

    /// <summary>Addresses a document to a client: the name in bold, then the company, the postal address, the e-mail and the VAT number.</summary>
    /// <param name="client">The client the document is for, read with its e-mail address.</param>
    /// <param name="labels">The words of the document, for the VAT number's prefix.</param>
    /// <returns>The party printed under the bill-to heading, empty lines removed.</returns>
    public static InvoicePdfParty ToPdfParty(this ClientBriefDto client, DocumentPdfLabels labels)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(labels);

        string?[] candidates =
        [
            client.CompanyName,
            client.Address.Street,
            client.Address.Address2,
            $"{client.Address.PostCode} {client.Address.City}".Trim(),
            client.Address.State,
            client.Address.Country,
            client.Email,
        ];
        var lines = candidates.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList();
        AddLabelled(lines, labels.VatNumber, client.Settings.TaxId?.Trim() ?? string.Empty);
        return new InvoicePdfParty(client.FullName, lines);
    }

    /// <summary>Adds a line for a value that is set, behind its label when it has one.</summary>
    /// <param name="lines">The lines of the party.</param>
    /// <param name="label">The word before the value, or null to print the value alone.</param>
    /// <param name="value">The value; nothing is added when it is empty.</param>
    private static void AddLabelled(List<string> lines, string? label, string value)
    {
        if (value.Length > 0)
        {
            lines.Add(label is null ? value : $"{label}: {value}");
        }
    }

    /// <summary>Turns the brand colour setting into an accent a document can print text in.</summary>
    /// <param name="brandPrimary">The <c>portal.brand.primary</c> setting as stored.</param>
    /// <returns>The colour as <c>#rrggbb</c>, darkened when it would be hard to read on white; null when none is set.</returns>
    private static string? AccentOf(string brandPrimary)
    {
        if (BrandPalette.Parse(brandPrimary) is not { } rgb)
        {
            return null;
        }

        var hex = BrandPalette.Format(rgb);
        return BrandPalette.ContrastRatio(hex, BrandPalette.White) >= AccentContrast ? hex : BrandPalette.Shade(hex, AccentFallbackStep);
    }
}
