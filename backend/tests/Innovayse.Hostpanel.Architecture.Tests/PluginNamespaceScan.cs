using System.Reflection;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Finds the types of an assembly that reference a reserved plugin namespace. The rule is a plain function of
/// the assembly, the reserved segments and the allow-list, so a test can run it on a planted type and on the
/// real layers with the same code.
/// </summary>
internal static class PluginNamespaceScan
{
    /// <summary>
    /// The types of the assembly that name a reserved namespace in a base type, interface, attribute, member
    /// signature or called method (nested types and compiler-generated closures included).
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="segments">The reserved second segments.</param>
    /// <param name="allowed">Namespace prefixes exempt from the reservation.</param>
    /// <returns>One line per offending type, naming the reserved type it references.</returns>
    public static IReadOnlyList<string> Offenders(Assembly assembly, IReadOnlyList<string> segments, IReadOnlyList<string> allowed)
    {
        var offenders = new List<string>();
        foreach (var type in Layers.TypesOf(assembly).Where(candidate => candidate.DeclaringType is null))
        {
            var reserved = ReferencedTypes(type)
                .Where(referenced => IsReserved(referenced.Namespace, segments, allowed))
                .Select(referenced => referenced.FullName ?? referenced.Name)
                .Distinct()
                .ToList();

            offenders.AddRange(reserved.Select(name => $"{type.FullName} -> {name}"));
        }

        return offenders;
    }

    /// <summary>The assemblies a layer references whose simple name sits under a reserved, non-allowed namespace.</summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="segments">The reserved second segments.</param>
    /// <param name="allowed">Namespace prefixes exempt from the reservation.</param>
    /// <returns>The offending assembly names.</returns>
    public static IReadOnlyList<string> ReservedReferences(Assembly assembly, IReadOnlyList<string> segments, IReadOnlyList<string> allowed)
    {
        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => IsReserved(name, segments, allowed))
            .ToList();
    }

    /// <summary>Whether a namespace is one of the reserved ones and not allow-listed.</summary>
    /// <param name="candidate">The namespace (or assembly simple name), or null for a type with none.</param>
    /// <param name="segments">The reserved second segments.</param>
    /// <param name="allowed">Namespace prefixes exempt from the reservation.</param>
    /// <returns><see langword="true"/> when the core may not reference it.</returns>
    public static bool IsReserved(string? candidate, IReadOnlyList<string> segments, IReadOnlyList<string> allowed)
    {
        if (candidate is null)
        {
            return false;
        }

        var parts = candidate.Split('.');
        if (parts.Length < 2 || parts[0] != ReservedPluginNamespaces.Root || !segments.Contains(parts[1]))
        {
            return false;
        }

        return !allowed.Any(prefix => candidate == prefix || candidate.StartsWith(prefix + ".", StringComparison.Ordinal));
    }

    /// <summary>Every type a top-level type and its nested types mention, plus the declaring types of the methods they call.</summary>
    /// <param name="type">The top-level type.</param>
    /// <returns>The referenced types, generic arguments unwrapped.</returns>
    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        foreach (var declared in MethodBodyReader.SelfAndNested(type))
        {
            var direct = new List<Type>();
            if (declared.BaseType is not null)
            {
                direct.Add(declared.BaseType);
            }

            direct.AddRange(declared.GetInterfaces());
            direct.AddRange(declared.CustomAttributes.Select(attribute => attribute.AttributeType));
            direct.AddRange(TypeSignatures.TypesNamedBy(declared));

            foreach (var referenced in direct.SelectMany(TypeSignatures.Unwrap))
            {
                yield return referenced;
            }
        }

        foreach (var call in MethodBodyReader.CallsMadeBy(type))
        {
            if (call.DeclaringType is not null)
            {
                yield return call.DeclaringType;
            }
        }
    }
}
