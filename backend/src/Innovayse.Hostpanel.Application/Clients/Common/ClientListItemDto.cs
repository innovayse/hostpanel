using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>Summary DTO for client list rows.</summary>
/// <param name="Id">Client primary key.</param>
/// <param name="UserId">Linked Identity user ID.</param>
/// <param name="Email">Email address from the linked Identity user.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name (null for individuals).</param>
/// <param name="Status">Current account status.</param>
/// <param name="IsUserDeleted">True if the linked Identity user no longer exists.</param>
/// <param name="TwoFactorEnabled">True if the user has TOTP 2FA enabled.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="ClientGroupId">The client group the client belongs to, null when it has none.</param>
/// <param name="ClientGroupName">The name of that group, null when the client has none.</param>
/// <param name="ClientGroupColor">The colour of that group as <c>#RRGGBB</c>, null when the client has none.</param>
public sealed record ClientListItemDto(
    int Id,
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName,
    ClientStatus Status,
    bool IsUserDeleted,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt,
    int? ClientGroupId,
    string? ClientGroupName,
    string? ClientGroupColor);
