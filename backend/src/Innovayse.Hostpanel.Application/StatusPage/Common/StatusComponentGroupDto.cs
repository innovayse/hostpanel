namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>A heading of the status page with its components in display order.</summary>
/// <param name="Id">The group's primary key.</param>
/// <param name="Name">The display name.</param>
/// <param name="Components">The components listed under the group, in display order.</param>
public sealed record StatusComponentGroupDto(int Id, string Name, IReadOnlyList<StatusComponentDto> Components);
