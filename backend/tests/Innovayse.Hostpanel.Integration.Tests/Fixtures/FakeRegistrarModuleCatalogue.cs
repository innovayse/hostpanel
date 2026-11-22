using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IRegistrarModuleCatalogue"/> for tests: a module names an installed registrar when the test listed it (any
/// non-blank module when the test listed none) and every module offers the capabilities the test gave, all of them unless a
/// test says otherwise. A test that needs the real manifests loads a real plugin registry instead.
/// </summary>
public sealed class FakeRegistrarModuleCatalogue : IRegistrarModuleCatalogue
{
    /// <summary>What every module offers.</summary>
    private readonly RegistrarCapabilitiesDto _capabilities;

    /// <summary>The installed registrar ids, or <see langword="null"/> when every non-blank module counts as installed.</summary>
    private readonly string[]? _installed;

    /// <summary>Initializes the catalogue.</summary>
    /// <param name="capabilities">What every module offers; every optional operation when <see langword="null"/>.</param>
    /// <param name="installed">The ids of the installed registrars; every non-blank module counts as installed when <see langword="null"/>.</param>
    public FakeRegistrarModuleCatalogue(RegistrarCapabilitiesDto? capabilities = null, string[]? installed = null)
    {
        _capabilities = capabilities ?? new RegistrarCapabilitiesDto(true, true, true);
        _installed = installed;
    }

    /// <inheritdoc/>
    public string? FindId(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return null;
        }

        var id = module.Trim();

        return _installed is null
            ? id
            : _installed.FirstOrDefault(known => string.Equals(known, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public RegistrarCapabilitiesDto CapabilitiesOf(string? module) => _capabilities;
}
