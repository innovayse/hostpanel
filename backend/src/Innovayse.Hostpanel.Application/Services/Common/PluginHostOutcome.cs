using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// What a plugin host access answered: the value, or why the host refused. Exactly one of the two is set; only the
/// factories build it. A refusal is a value, not an exception, inside the host; the host API turns it into the SDK's
/// <see cref="PluginHostAccessException"/> at the plugin boundary.
/// </summary>
/// <typeparam name="T">What the access hands the plugin on success; it may carry a secret, so it is never printed.</typeparam>
public sealed class PluginHostOutcome<T>
    where T : class
{
    /// <summary>The granted value, carrying any secret once; <see langword="null"/> on a refusal.</summary>
    public T? Value { get; }

    /// <summary>Why the host refused; <see langword="null"/> when the access was granted.</summary>
    public PluginHostAccessFailure? Failure { get; }

    /// <summary>Builds an outcome; only the factories call it.</summary>
    /// <param name="value">The granted value, or null.</param>
    /// <param name="failure">The refusal, or null.</param>
    private PluginHostOutcome(T? value, PluginHostAccessFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    /// <summary>The access was granted.</summary>
    /// <param name="value">What the plugin receives.</param>
    /// <returns>A successful outcome.</returns>
    public static PluginHostOutcome<T> Granted(T value)
    {
        return new PluginHostOutcome<T>(value, null);
    }

    /// <summary>The host refused.</summary>
    /// <param name="failure">Why.</param>
    /// <returns>A refused outcome.</returns>
    public static PluginHostOutcome<T> Refused(PluginHostAccessFailure failure)
    {
        return new PluginHostOutcome<T>(null, failure);
    }

    /// <summary>Describes the outcome without the value, so logging it never leaks a secret the value carries.</summary>
    /// <returns>The kind of value granted or the refusal.</returns>
    public override string ToString()
    {
        return Value is not null ? $"Granted {typeof(T).Name}" : $"Refused {Failure}";
    }
}
