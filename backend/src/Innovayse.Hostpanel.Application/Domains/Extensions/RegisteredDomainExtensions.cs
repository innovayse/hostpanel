using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Application.Domains.Extensions;

/// <summary>Extension methods that turn a stored <see cref="RegisteredDomain"/> into what a registrar call carries.</summary>
public static class RegisteredDomainExtensions
{
    /// <summary>
    /// Names the domain the way a registrar plugin addresses it: by its name and the reference the registrar returned. A domain
    /// that never got a reference is addressed by its name, which is then the only address the host has.
    /// </summary>
    /// <param name="domain">The stored domain.</param>
    /// <returns>The domain's address at the registrar.</returns>
    public static DomainRef ToRef(this RegisteredDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new DomainRef(domain.Name, domain.RegistrarRef ?? domain.Name);
    }
}
