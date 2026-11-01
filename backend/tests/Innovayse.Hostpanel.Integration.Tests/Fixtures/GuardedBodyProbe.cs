using Innovayse.Hostpanel.API.Plugins;
using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>The probe with both request guards the production plugin doors carry.</summary>
[Route("guarded")]
[PluginBodyLimit]
[NoFormBinding]
[Controller]
public sealed class GuardedBodyProbe : BodyProbe
{
    /// <summary>Initializes a new instance of the <see cref="GuardedBodyProbe"/> class.</summary>
    /// <param name="options">The configured body cap.</param>
    public GuardedBodyProbe(IOptions<PluginApiOptions> options)
        : base(options)
    {
    }
}
