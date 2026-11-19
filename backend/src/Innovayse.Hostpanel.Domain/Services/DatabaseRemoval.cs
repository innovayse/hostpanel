using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// What a hosting provider is asked to drop. Built by HostPanel from the recorded <see cref="ServiceDatabase"/>, only
/// after <see cref="HostingResource.IsBoundTo"/> confirmed the account is still the one it was created for.
/// </summary>
/// <param name="Account">The account the database belongs to.</param>
/// <param name="DatabaseId">The database's stable identifier.</param>
/// <param name="Label">The label the database was created for, which is how the provider finds it.</param>
/// <param name="Name">The database name on the server.</param>
/// <param name="Username">The database user's name on the server, dropped with the database.</param>
public sealed record DatabaseRemoval(SshAccount Account, Guid DatabaseId, string Label, string Name, string Username);
