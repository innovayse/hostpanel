using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// What a hosting provider is asked to take down. Built by HostPanel from the recorded <see cref="ServiceWebsite"/>,
/// only after <see cref="HostingResource.IsBoundTo"/> confirmed the account is still the one it was created for.
/// </summary>
/// <param name="Account">The account the website belongs to.</param>
/// <param name="WebsiteId">The website's stable identifier.</param>
/// <param name="Hostname">The host name the website answers on.</param>
public sealed record WebsiteRemoval(SshAccount Account, Guid WebsiteId, string Hostname);
