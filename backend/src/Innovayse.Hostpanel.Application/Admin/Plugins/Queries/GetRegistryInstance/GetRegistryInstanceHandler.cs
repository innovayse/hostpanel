using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryInstance;

/// <summary>Handles <see cref="GetRegistryInstanceQuery"/> by reading the stored identity and wording its last failure.</summary>
public sealed class GetRegistryInstanceHandler
{
    /// <summary>Reads the registration state.</summary>
    private readonly RegistryInstanceEnrollment _enrollment;

    /// <summary>Initializes the handler.</summary>
    /// <param name="enrollment">Reads the registration state.</param>
    public GetRegistryInstanceHandler(RegistryInstanceEnrollment enrollment)
    {
        _enrollment = enrollment;
    }

    /// <summary>Returns the registration state.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the state; this use case refuses nothing.</returns>
    public async Task<Result<RegistryInstanceDto>> HandleAsync(GetRegistryInstanceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Result<RegistryInstanceDto>.Success(await _enrollment.GetStatusAsync(ct));
    }
}
