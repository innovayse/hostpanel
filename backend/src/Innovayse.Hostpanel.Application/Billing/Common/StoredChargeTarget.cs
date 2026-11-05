using Innovayse.Hostpanel.Domain.Billing;
using Hostpanel.Sdk.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The consent a stored charge will use, with the plugin that holds its method and the client's customer there.</summary>
/// <param name="Consent">The chosen consent, or <see langword="null"/> when none is usable.</param>
/// <param name="Plugin">The module's stored credential capability, or <see langword="null"/> when none is usable.</param>
/// <param name="CustomerRef">The client's customer reference at the module.</param>
/// <param name="Seen">Whether a consented module was usable (its capability resolved), even if the client has no customer there.</param>
public sealed record StoredChargeTarget(CardChargeConsent? Consent, IStoredCredentialPlugin? Plugin, string? CustomerRef, bool Seen);
