using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;

namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>How one field of a hosting account compares between the platform and the panel.</summary>
/// <param name="Field">The field compared.</param>
/// <param name="Kind">Whether the two agree, differ in a way an operation can fix, or cannot be reconciled.</param>
/// <param name="PanelValue">What the panel reports for the field, or <see langword="null"/> when it reports nothing or the field is never reported.</param>
public sealed record HostedAccountDrift(PanelSyncField Field, HostedAccountDriftKind Kind, string? PanelValue);
