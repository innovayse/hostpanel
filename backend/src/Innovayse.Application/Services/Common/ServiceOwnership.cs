namespace Innovayse.Application.Services.Common;

using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Resolves client-service ownership against the client the current credential names.
/// </summary>
/// <param name="services">Client service repository.</param>
/// <param name="clients">Client repository, for mapping the caller's subject to their account.</param>
/// <param name="caller">Who is asking. Nothing tells this type whose services to consider.</param>
public sealed class ServiceOwnership(
    IClientServiceRepository services,
    IClientRepository clients,
    ICurrentRequestContext caller) : IServiceOwnership
{
    /// <inheritdoc/>
    public async Task RequireOwnedByCallerAsync(int serviceId, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clients.FindByUserIdAsync(userId, ct);
        if (client is not null)
        {
            var service = await services.FindByIdAsync(serviceId, ct);
            if (service is not null && service.ClientId == client.Id)
            {
                return;
            }
        }

        // A service that does not exist, a service belonging to somebody else, and a caller with
        // no client record all land here and answer identically. Distinguishing them would turn
        // this route into a way of enumerating ids -- and service ids are sequential integers.
        throw new MyServiceNotFoundException(serviceId);
    }

    /// <inheritdoc/>
    public async Task<int> RequireCallerClientIdAsync(CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clients.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        return client.Id;
    }
}
