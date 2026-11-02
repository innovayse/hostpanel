using Innovayse.Hostpanel.API.Plugins;
using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>The probe with the body limit guard only, to show what the form guard is for.</summary>
[Route("limit-only")]
[PluginBodyLimit]
[Controller]
public sealed class LimitOnlyBodyProbe : BodyProbe
{
    /// <summary>Initializes a new instance of the <see cref="LimitOnlyBodyProbe"/> class.</summary>
    /// <param name="options">The configured body cap.</param>
    public LimitOnlyBodyProbe(IOptions<PluginApiOptions> options)
        : base(options)
    {
    }
}
