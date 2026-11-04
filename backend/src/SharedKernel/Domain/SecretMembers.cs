using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace Innovayse.Hostpanel.SharedKernel.Domain;

/// <summary>
/// The one definition of which members of a type hold a secret, and the one way a record prints
/// itself without them.
/// </summary>
/// <remarks>
/// <para>
/// A positional record's generated <c>ToString</c> writes every property, so a record that carries a
/// private key, a password or a token leaks it into any log line, exception message or debugger view
/// that formats the value. A record that carries such a member overrides <c>PrintMembers</c> (the
/// method the generated <c>ToString</c> calls) with a single line that delegates to
/// <see cref="Print"/>, which writes every public property and replaces the value of each secret one
/// with <see cref="Mask"/>. A secret is a property whose name contains a fragment such as
/// <c>Password</c> or <c>Token</c>, or one marked <see cref="SecretMemberAttribute"/> when its name
/// gives nothing away. The architecture tests use <see cref="IsSecret"/> to find every record
/// that needs the override, so the printer and the rule can never disagree about what a secret is.
/// </para>
/// <para>
/// Only <c>ToString</c> changes. Equality, serialization and every property stay as the record
/// declares them.
/// </para>
/// </remarks>
public static class SecretMembers
{
    /// <summary>What is printed in place of a secret value.</summary>
    public const string Mask = "***";

    /// <summary>The printable properties of each type already seen, so the reflection runs once per type.</summary>
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Property, bool Secret)[]> PrintableMembers = new();

    /// <summary>Name fragments that mark a member as holding a secret, compared case-insensitively.</summary>
    private static readonly string[] SecretNameFragments =
    [
        "PrivateKey",
        "Password",
        "Passphrase",
        "Secret",
        "Token",
        "ApiKey",
        "AccessKey",
        "AccessHash",
        "SigningKey",
        "LicenseKey",
        "EppCode",
        "AuthCode",
    ];

    /// <summary>
    /// Tells whether a property holds a secret: its name contains a secret fragment and its type can
    /// carry text or bytes. Flags, counts, dates and enums named after a secret (<c>HasPassword</c>,
    /// <c>TokenExpiresAt</c>) describe it and do not hold it.
    /// </summary>
    /// <param name="property">The property to classify.</param>
    /// <returns><see langword="true"/> when printing the value would reveal a secret.</returns>
    public static bool IsSecret(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.IsDefined(typeof(SecretMemberAttribute), true))
        {
            return true;
        }

        if (!SecretNameFragments.Any(fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return !(type.IsPrimitive || type.IsEnum || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan));
    }

    /// <summary>
    /// Writes the public instance properties of a value as <c>Name = value</c> pairs, with every secret
    /// property printed as <see cref="Mask"/>. Matches the contract of a record's <c>PrintMembers</c>.
    /// </summary>
    /// <param name="instance">The value being printed.</param>
    /// <param name="builder">The builder the record's <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    public static bool Print(object instance, StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(builder);

        var written = false;
        foreach (var (property, secret) in PrintableMembers.GetOrAdd(instance.GetType(), Classify))
        {
            if (written)
            {
                builder.Append(", ");
            }

            builder.Append(property.Name).Append(" = ");
            if (secret)
            {
                // A null secret is still reported as unset: that is not the secret, and it helps diagnosis.
                builder.Append(property.GetValue(instance) is null ? "null" : Mask);
            }
            else
            {
                builder.Append(property.GetValue(instance));
            }

            written = true;
        }

        return written;
    }

    /// <summary>
    /// Lists the readable public instance properties of a type with their secret flag. Run once per
    /// type; <see cref="PrintableMembers"/> keeps the result so printing never repeats the reflection.
    /// </summary>
    /// <param name="type">The type being printed.</param>
    /// <returns>Each printable property and whether it holds a secret.</returns>
    private static (PropertyInfo Property, bool Secret)[] Classify(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null)
            .Select(property => (property, IsSecret(property)))
            .ToArray();
    }
}
