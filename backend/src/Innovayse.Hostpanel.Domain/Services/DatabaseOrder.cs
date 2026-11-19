using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// What a hosting provider is asked to set up for a new database. Built by HostPanel from the service record and the
/// <see cref="ServiceDatabase"/> it just recorded, never from anything a browser sent.
/// </summary>
/// <param name="Account">The account the database belongs to.</param>
/// <param name="DatabaseId">The database's stable identifier, so the provider can find it again.</param>
/// <param name="Label">The label the plugin gave the database.</param>
public sealed record DatabaseOrder(SshAccount Account, Guid DatabaseId, string Label);
