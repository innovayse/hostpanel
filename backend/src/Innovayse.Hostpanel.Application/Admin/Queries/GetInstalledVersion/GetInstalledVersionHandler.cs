using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetInstalledVersion;

/// <summary>
/// Handles <see cref="GetInstalledVersionQuery"/> by reading the running version from
/// <see cref="IInstalledVersionProvider"/>. No database or outbound call is made.
/// </summary>
public sealed class GetInstalledVersionHandler
{
    /// <summary>Returns the version of the running build.</summary>
    private readonly IInstalledVersionProvider _versionProvider;

    /// <summary>Initializes the handler.</summary>
    /// <param name="versionProvider">Returns the version of the running build.</param>
    public GetInstalledVersionHandler(IInstalledVersionProvider versionProvider)
    {
        _versionProvider = versionProvider;
    }

    /// <summary>Reads the installed version.</summary>
    /// <param name="query">The query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the installed version. This use case refuses nothing.</returns>
    public Task<Result<InstalledVersionDto>> HandleAsync(GetInstalledVersionQuery query, CancellationToken ct)
    {
        _ = query;
        _ = ct;

        return Task.FromResult(Result<InstalledVersionDto>.Success(new InstalledVersionDto(_versionProvider.Version)));
    }
}
