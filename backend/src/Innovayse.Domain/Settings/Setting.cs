namespace Innovayse.Domain.Settings;

using Innovayse.Domain.Common;

/// <summary>Represents a system-wide configuration key-value setting.</summary>
public sealed class Setting : AggregateRoot
{
    /// <summary>Gets the unique configuration key.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the configuration value.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets the optional description of this setting.</summary>
    public string? Description { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Setting() : base(0) { }

    /// <summary>
    /// Creates a new setting with the given key and value.
    /// </summary>
    /// <param name="key">The unique configuration key.</param>
    /// <param name="value">The configuration value.</param>
    /// <param name="description">Optional description of this setting.</param>
    /// <returns>A new, unpersisted <see cref="Setting"/> instance.</returns>
    public static Setting Create(string key, string value, string? description) =>
        new() { Key = key, Value = value, Description = description };

    /// <summary>
    /// Updates the configuration value.
    /// </summary>
    /// <param name="value">The new value to store.</param>
    public void UpdateValue(string value) => Value = value;
}
