using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryChallenge;

/// <summary>Handles <see cref="GetRegistryChallengeQuery"/> by answering exactly the challenge stored for the instance id, and nothing for any other id.</summary>
public sealed class GetRegistryChallengeHandler
{
    /// <summary>Reads the stored identity.</summary>
    private readonly RegistryInstanceStore _store;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Reads the stored identity.</param>
    public GetRegistryChallengeHandler(RegistryInstanceStore store)
    {
        _store = store;
    }

    /// <summary>Returns the challenge for the id.</summary>
    /// <param name="query">The instance id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The challenge text exactly as the registry gave it; <c>PLUGIN_REGISTRY_INSTANCE_NOT_FOUND</c> for any other id, or before a registration succeeded.</returns>
    public async Task<Result<string>> HandleAsync(GetRegistryChallengeQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var record = await _store.LoadAsync(ct);
        return record is { InstanceId: { } id, Challenge: { Length: > 0 } challenge } && id == query.InstanceId
            ? Result<string>.Success(challenge)
            : Result<string>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_FOUND);
    }
}
