using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// The demo has no registrar plugin installed, so every module value names the one fake registrar and that registrar offers
/// every optional operation, which lets the whole domain screen be shown.
/// </summary>
public sealed class DemoRegistrarModuleCatalogue : IRegistrarModuleCatalogue
{
    /// <summary>The module the demo's TLDs are configured for.</summary>
    public const string ModuleId = "demo";

    /// <inheritdoc/>
    public string? FindId(string module)
    {
        return string.IsNullOrWhiteSpace(module) ? null : module.Trim();
    }

    /// <inheritdoc/>
    public RegistrarCapabilitiesDto CapabilitiesOf(string? module)
    {
        return new RegistrarCapabilitiesDto(true, true, true);
    }
}
