using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;

namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>What a panel sync did about one field of a hosting account.</summary>
/// <param name="Field">The field compared.</param>
/// <param name="Outcome">What the sync did about it.</param>
/// <param name="PanelValue">What the panel reported for the field, or <see langword="null"/> when it reported nothing; never the password.</param>
public sealed record ServiceSyncFieldDto(PanelSyncField Field, PanelSyncOutcome Outcome, string? PanelValue);
