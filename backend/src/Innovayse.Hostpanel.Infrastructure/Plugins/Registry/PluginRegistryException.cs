namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// Raised inside <see cref="PluginRegistryClient"/> to stop a call with a <c>PLUGIN_REGISTRY_*</c> code. It never leaves
/// the client: the public methods turn it into a failed result, and its message carries nothing from the registry.
/// </summary>
internal sealed class PluginRegistryException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="code">The <c>PLUGIN_REGISTRY_*</c> error code the call answers with.</param>
    public PluginRegistryException(string code)
        : base(code)
    {
        Code = code;
    }

    /// <summary>The <c>PLUGIN_REGISTRY_*</c> error code the call answers with.</summary>
    public string Code { get; }
}
