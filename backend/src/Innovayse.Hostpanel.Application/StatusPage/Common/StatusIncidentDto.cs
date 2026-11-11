using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>An incident announced on the status page, with its timeline.</summary>
/// <param name="Id">The incident's primary key.</param>
/// <param name="Title">The headline visitors see.</param>
/// <param name="State">Where the incident stands now.</param>
/// <param name="StartedAt">The UTC moment the incident was announced.</param>
/// <param name="ResolvedAt">The UTC moment the incident was resolved, or <c>null</c> while it is open.</param>
/// <param name="Updates">The timeline entries, newest first.</param>
public sealed record StatusIncidentDto(
    int Id,
    string Title,
    StatusIncidentState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<StatusIncidentUpdateDto> Updates);
