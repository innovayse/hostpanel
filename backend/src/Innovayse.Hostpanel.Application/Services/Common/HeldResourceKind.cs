using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>What differs between the kinds of resource a plugin holds on a hosting service when one is removed.</summary>
/// <typeparam name="TResource">The kind of resource: a website or a database.</typeparam>
/// <param name="Name">The kind as the activity line and the log name it, for example <c>website</c>.</param>
/// <param name="IdOf">Reads the id the plugin knows the resource by.</param>
/// <param name="LabelOf">Reads what the activity line calls the resource, for example its host name; never a secret.</param>
/// <param name="RemoveOnServer">Asks the server's provider, for the account, to take the resource down.</param>
internal sealed record HeldResourceKind<TResource>(
    string Name,
    Func<TResource, Guid> IdOf,
    Func<TResource, string> LabelOf,
    Func<IHostingResourceProvider, SshAccount, TResource, Task> RemoveOnServer)
    where TResource : HostingResource;
