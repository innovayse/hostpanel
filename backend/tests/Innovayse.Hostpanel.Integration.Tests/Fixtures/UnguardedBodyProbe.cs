using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>The probe with no request guard, to show what the web server does by default.</summary>
[Route("unguarded")]
[Controller]
public sealed class UnguardedBodyProbe : BodyProbe
{
    /// <summary>Initializes a new instance of the <see cref="UnguardedBodyProbe"/> class.</summary>
    /// <param name="options">The configured body cap.</param>
    public UnguardedBodyProbe(IOptions<PluginApiOptions> options)
        : base(options)
    {
    }
}
