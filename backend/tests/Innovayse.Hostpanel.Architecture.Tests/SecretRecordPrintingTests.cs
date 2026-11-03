using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// A record's generated <c>ToString</c> writes every property, so a record that carries a private key,
/// password, token or other secret prints it into any log line, exception message or debugger view that
/// formats the value. Every such record, in every layer, must override <c>PrintMembers</c> (normally
/// through <see cref="SecretMembers.Print"/>) so the generated <c>ToString</c> masks the secret. A
/// secret is a property the name rule matches or one marked <see cref="SecretMemberAttribute"/>; both
/// are treated alike here because <see cref="SecretMembers.IsSecret"/> answers for both.
/// </summary>
public sealed class SecretRecordPrintingTests
{
    /// <summary>
    /// Secret-named properties that are not actually secret, keyed by record full name and property
    /// name, with the reason. Keep this empty unless the property is public by design; a record that
    /// really carries a secret is fixed, never listed here.
    /// </summary>
    private static readonly IReadOnlyDictionary<(string Type, string Property), string> NotSecret =
        new Dictionary<(string Type, string Property), string>
        {
            [("Innovayse.Hostpanel.Application.Demo.Common.DemoAccountDto", "Password")] =
                "The demo sign-in password is published on the sign-in page by design (GET /api/public/demo); it is not a secret.",
        };

    /// <summary>
    /// Records whose constructor validation refuses the sentinel values, keyed by full name, with the
    /// reason. The behavioural check cannot build them, so the structural rule alone covers them.
    /// A record that cannot be built and is not listed here fails the test, so nothing is skipped silently.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RefusesSentinels =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Every layer and plugin assembly the rule covers.</summary>
    private static readonly Assembly[] Assemblies =
    [
        Layers.SharedKernel,
        Layers.Domain,
        Layers.Shared,
        Layers.Application,
        Layers.Infrastructure,
        Layers.Api,
        Layers.Sdk,
        Layers.ProviderCwp,
        Layers.ProviderCwp7,
        Layers.ProviderInecobank,
    ];

    /// <summary>A record with a secret property declares its own <c>PrintMembers</c>.</summary>
    [Fact]
    public void RecordsWithSecretProperties_OverridePrintMembers()
    {
        var offenders = Assemblies
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(IsRecord)
            .Where(type => SecretProperties(type).Any() && !DeclaresPrintMembers(type))
            .Select(type => $"{type.FullName} ({string.Join(", ", SecretProperties(type).Select(p => p.Name))})")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        string.Join(Environment.NewLine, offenders).Should().BeEmpty(
            "a record's generated ToString prints every property; override PrintMembers with SecretMembers.Print");
    }

    /// <summary>
    /// A record deriving from a record that masks secrets must not fall back to the compiler-generated
    /// printer, which would print the base's secrets again: it declares its own <c>PrintMembers</c> that
    /// calls <see cref="SecretMembers.Print"/>, or it declares no members of its own.
    /// </summary>
    [Fact]
    public void RecordsDerivedFromMaskingRecords_MaskToo()
    {
        var offenders = Assemblies
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(IsRecord)
            .Where(type => type.BaseType is { } baseType && IsRecord(baseType) && MasksSecrets(baseType))
            .Where(type => HasOwnMembers(type) && !CallsSecretMembersPrint(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        string.Join(Environment.NewLine, offenders).Should().BeEmpty(
            "a record derived from a masking record needs its own PrintMembers calling SecretMembers.Print, or no members of its own");
    }

    /// <summary>
    /// Behaviour behind the structural rule: a record built with a recognisable value in each of its
    /// secret members prints neither the value through <c>ToString</c> or string interpolation, and shows
    /// the mask in its place. At least one sentinel must be placed per record, so an empty construction
    /// cannot pass.
    /// </summary>
    [Fact]
    public void RecordsWithSecretProperties_NeverPrintTheSecretValue()
    {
        var problems = new List<string>();
        foreach (var type in SecretRecords())
        {
            var built = Build(type, out var placed, out var failure);
            if (failure is not null)
            {
                if (!RefusesSentinels.ContainsKey(type.FullName!))
                {
                    problems.Add($"{type.FullName} could not be built: {failure}");
                }

                continue;
            }

            if (placed.Count == 0)
            {
                problems.Add($"{type.FullName} had no sentinel placed; extend Build for its secret member types");
                continue;
            }

            var printed = new[] { built!.ToString()!, $"{built}" };
            foreach (var (property, sentinel) in placed)
            {
                if (printed.Any(text => text.Contains(sentinel, StringComparison.Ordinal)))
                {
                    problems.Add($"{type.FullName} prints the value of {property}");
                }

                // A record may leave a secret out of its text altogether (an identity described by a public
                // fingerprint); when it does print the member, the value must be the mask.
                if (printed[0].Contains($"{property} = ", StringComparison.Ordinal)
                    && !printed[0].Contains($"{property} = {SecretMembers.Mask}", StringComparison.Ordinal))
                {
                    problems.Add($"{type.FullName} prints {property} without the mask");
                }
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty("a secret must never reach a log line through ToString");
    }

    /// <summary>An allow-list entry still names a record that has that secret-named property, so it cannot go stale.</summary>
    [Fact]
    public void NotSecretAllowList_HasNoStaleEntries()
    {
        var records = Assemblies.SelectMany(Layers.AuthoredTypesOf).Where(IsRecord).ToDictionary(type => type.FullName!);

        foreach (var ((typeName, propertyName), reason) in NotSecret)
        {
            reason.Should().NotBeNullOrWhiteSpace("every exception states why the property is not secret");
            records.Should().ContainKey(typeName);
            records[typeName]
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(SecretMembers.IsSecret)
                .Select(property => property.Name)
                .Should().Contain(propertyName, "{0}.{1} no longer needs an exception", typeName, propertyName);
        }

        foreach (var (typeName, reason) in RefusesSentinels)
        {
            reason.Should().NotBeNullOrWhiteSpace("every skipped record states why its constructor refuses the sentinels");
            records.Should().ContainKey(typeName);
        }
    }

    /// <summary>Tells whether a type is a record: the compiler gives every record class a <c>&lt;Clone&gt;$</c> method.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for a record class.</returns>
    private static bool IsRecord(Type type)
    {
        return type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;
    }

    /// <summary>The properties a type declares itself that hold a secret and are not allow-listed.</summary>
    /// <param name="type">The record type.</param>
    /// <returns>Its secret properties.</returns>
    private static IEnumerable<PropertyInfo> SecretProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(SecretMembers.IsSecret)
            .Where(property => !NotSecret.ContainsKey((type.FullName!, property.Name)));
    }

    /// <summary>
    /// Tells whether a record has a hand-written <c>PrintMembers</c>; the compiler-synthesised one is
    /// marked <see cref="CompilerGeneratedAttribute"/> and prints every property.
    /// </summary>
    /// <param name="type">The record type.</param>
    /// <returns><see langword="true"/> when the record overrides it.</returns>
    private static bool DeclaresPrintMembers(Type type)
    {
        var method = type.GetMethod(
            "PrintMembers",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            [typeof(StringBuilder)]);

        return method is not null && !method.IsDefined(typeof(CompilerGeneratedAttribute), false);
    }

    /// <summary>Tells whether a record, or a record it derives from, masks secrets through a hand-written printer.</summary>
    /// <param name="type">The record type.</param>
    /// <returns><see langword="true"/> when printing the record hides a secret.</returns>
    private static bool MasksSecrets(Type type)
    {
        for (var current = type; current is not null && IsRecord(current); current = current.BaseType)
        {
            if (DeclaresPrintMembers(current))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tells whether a record declares an instance property of its own.</summary>
    /// <param name="type">The record type.</param>
    /// <returns><see langword="true"/> when the record adds members to what it inherits.</returns>
    private static bool HasOwnMembers(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(property => property.Name != "EqualityContract");
    }

    /// <summary>Tells whether the compiled record calls <see cref="SecretMembers.Print"/>.</summary>
    /// <param name="type">The record type.</param>
    /// <returns><see langword="true"/> when its own <c>PrintMembers</c> delegates to the shared helper.</returns>
    private static bool CallsSecretMembersPrint(Type type)
    {
        return DeclaresPrintMembers(type)
            && MethodBodyReader.CallsMadeBy(type)
                .Any(called => called.DeclaringType == typeof(SecretMembers) && called.Name == nameof(SecretMembers.Print));
    }

    /// <summary>The concrete records in scope for the behaviour check: those with a secret property that are not allow-listed.</summary>
    /// <returns>The record types.</returns>
    private static IEnumerable<Type> SecretRecords()
    {
        return Assemblies
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(IsRecord)
            .Where(type => !type.IsAbstract)
            .Where(type => SecretProperties(type).Any());
    }

    /// <summary>
    /// Creates a record through its widest constructor, passing a recognisable value for every
    /// secret-bearing parameter (string, byte array, string list), a plain value for other strings and a
    /// default for the rest. Secret init-only properties no parameter covers are set afterwards.
    /// </summary>
    /// <param name="type">The record type.</param>
    /// <param name="placed">Each secret member that received a sentinel, with the text to look for in the output.</param>
    /// <param name="failure">Why the instance could not be built; <see langword="null"/> when it was.</param>
    /// <returns>The instance, or <see langword="null"/> when the constructor refused the values.</returns>
    private static object? Build(Type type, out List<(string Property, string Sentinel)> placed, out string? failure)
    {
        var passed = new List<(string Property, string Sentinel)>();
        placed = passed;
        failure = null;

        var constructor = type
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(candidate => !candidate.GetParameters().Any(parameter => parameter.ParameterType == type))
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();

        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = constructor.GetParameters().Select<ParameterInfo, object?>(parameter =>
        {
            // A parameter that is not a property (a helper value the constructor consumes) is never a secret here.
            var property = PropertyNamed(type, parameter.Name!);
            if (property is not null && SecretMembers.IsSecret(property) && !NotSecret.ContainsKey((type.FullName!, property.Name)))
            {
                var value = SentinelFor(parameter.ParameterType, parameter.Name!, out var text);
                if (value is not null)
                {
                    covered.Add(property.Name);
                    passed.Add((property.Name, text));
                    return value;
                }
            }

            if (parameter.ParameterType == typeof(string))
            {
                return $"plain-{parameter.Name}";
            }

            return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
        }).ToArray();

        object instance;
        try
        {
            instance = constructor.Invoke(arguments);
        }
        catch (TargetInvocationException exception)
        {
            failure = exception.InnerException?.Message ?? exception.Message;
            return null;
        }

        foreach (var property in SecretProperties(type).Where(property => !covered.Contains(property.Name) && property.SetMethod is not null))
        {
            var value = SentinelFor(property.PropertyType, property.Name, out var text);
            if (value is not null)
            {
                property.SetValue(instance, value);
                passed.Add((property.Name, text));
            }
        }

        return instance;
    }

    /// <summary>Makes a recognisable secret value for the member types a record can hold a secret in.</summary>
    /// <param name="memberType">The parameter or property type.</param>
    /// <param name="name">The member name, so each sentinel is unique.</param>
    /// <param name="text">The text a leak of the value would show up as in the output.</param>
    /// <returns>The value, or <see langword="null"/> when the type is not one the check understands.</returns>
    private static object? SentinelFor(Type memberType, string name, out string text)
    {
        text = $"sentinel-{name}";
        if (memberType == typeof(string))
        {
            return text;
        }

        if (memberType == typeof(byte[]))
        {
            return Encoding.UTF8.GetBytes(text);
        }

        if (memberType == typeof(IReadOnlyList<string>) || memberType == typeof(IEnumerable<string>)
            || memberType == typeof(IReadOnlyCollection<string>) || memberType == typeof(string[]))
        {
            return new[] { text };
        }

        return null;
    }

    /// <summary>Finds the property a positional parameter became, ignoring case.</summary>
    /// <param name="type">The record type.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The property, or <see langword="null"/> when the parameter is not a property.</returns>
    private static PropertyInfo? PropertyNamed(Type type, string name)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
