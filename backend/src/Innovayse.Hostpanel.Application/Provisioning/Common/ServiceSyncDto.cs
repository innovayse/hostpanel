namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>The result of reconciling a service's hosting account with its panel, field by field.</summary>
/// <param name="Fields">One entry for each field the sync compared.</param>
public sealed record ServiceSyncDto(IReadOnlyList<ServiceSyncFieldDto> Fields);
