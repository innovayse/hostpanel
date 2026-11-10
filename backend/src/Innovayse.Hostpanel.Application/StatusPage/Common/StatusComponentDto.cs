using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>One component of the status page and the state it is in.</summary>
/// <param name="Id">The component's primary key.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">A short explanation of what the component covers, or <c>null</c>.</param>
/// <param name="State">The state the page shows for the component now.</param>
public sealed record StatusComponentDto(int Id, string Name, string? Description, StatusComponentState State);
