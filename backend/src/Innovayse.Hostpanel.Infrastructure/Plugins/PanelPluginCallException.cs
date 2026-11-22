namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// A panel plugin threw while the host called it. It carries the plugin id, the operation and the type of what was thrown, never
/// the plugin's own message or the original exception: that text may hold a URL, a key or a server's answer, and a handler that
/// logs this exception must not print it.
/// </summary>
public sealed class PanelPluginCallException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PanelPluginCallException"/> class.</summary>
    /// <param name="pluginId">The plugin that threw.</param>
    /// <param name="operation">The contract method that was called.</param>
    /// <param name="exceptionType">The type name of what the plugin threw.</param>
    public PanelPluginCallException(string pluginId, string operation, string exceptionType)
        : base($"Plugin '{pluginId}' failed in {operation} with {exceptionType}.")
    {
        PluginId = pluginId;
        Operation = operation;
        ExceptionType = exceptionType;
    }

    /// <summary>Gets the plugin that threw.</summary>
    public string PluginId { get; }

    /// <summary>Gets the contract method that was called.</summary>
    public string Operation { get; }

    /// <summary>Gets the type name of what the plugin threw.</summary>
    public string ExceptionType { get; }
}
