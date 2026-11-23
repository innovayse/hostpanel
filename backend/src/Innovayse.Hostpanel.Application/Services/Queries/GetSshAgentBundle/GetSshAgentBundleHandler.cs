using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetSshAgentBundle;

/// <summary>Handles <see cref="GetSshAgentBundleQuery"/>.</summary>
public sealed class GetSshAgentBundleHandler
{
    /// <summary>The bundles this build can serve.</summary>
    private readonly ISshAgentBundles _bundles;

    /// <summary>Initialises the handler.</summary>
    /// <param name="bundles">The bundles this build can serve.</param>
    public GetSshAgentBundleHandler(ISshAgentBundles bundles)
    {
        _bundles = bundles;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <returns>The bundle as a download, or <c>SSH_AGENT_PROFILE_NOT_FOUND</c> when no loaded plugin ships the profile.</returns>
    public Result<FileDownloadDto> Handle(GetSshAgentBundleQuery query)
    {
        var bundle = query.Profile is null ? _bundles.BuiltIn : _bundles.For(query.Profile);
        if (bundle is null)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.SSH_AGENT_PROFILE_NOT_FOUND);
        }

        return Result<FileDownloadDto>.Success(new FileDownloadDto(
            bundle.Content.ToArray(), "application/gzip", $"hostpanel-ssh-key-agent-{bundle.Version}.tar.gz"));
    }
}
