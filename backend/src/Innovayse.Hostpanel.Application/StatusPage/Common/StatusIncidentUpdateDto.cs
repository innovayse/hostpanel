using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>One entry of an incident's timeline.</summary>
/// <param name="Id">The entry's primary key.</param>
/// <param name="Message">What the operator told the visitors.</param>
/// <param name="State">The state the incident moved to with this entry.</param>
/// <param name="CreatedAt">The UTC moment the entry was posted.</param>
public sealed record StatusIncidentUpdateDto(int Id, string Message, StatusIncidentState State, DateTimeOffset CreatedAt);
