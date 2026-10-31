using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Innovayse.Hostpanel.Architecture.Tests.Planted;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// <see cref="RangeAttribute"/> converts bounds given as strings lazily and not thread-safely, so
/// <c>[Range(typeof(TimeSpan), ...)]</c> throws <see cref="InvalidCastException"/> when several hosts validate
/// at once (issue 756). The rule refuses any <see cref="RangeAttribute"/> operand other than int and double;
/// options with such bounds are checked by an options validator instead.
/// </summary>
public sealed class NoLazyRangeTests
{
    /// <summary>No property or field in the authored assemblies uses a range whose operand is not int or double.</summary>
    [Fact]
    public void RangeAttribute_UsesOnlyIntOrDoubleOperands()
    {
        var offenders = Offenders([Layers.Domain, Layers.Shared, Layers.Application, Layers.Infrastructure, Layers.Api]);

        offenders.Should().BeEmpty("a typed Range converts lazily and is not thread-safe; validate the bound in an IValidateOptions instead");
    }

    /// <summary>The same scan reports a planted type with a TimeSpan range, so the rule is proven to bite.</summary>
    [Fact]
    public void Rule_FailsOnPlantedTimeSpanRange()
    {
        var offenders = Offenders([typeof(PlantedLazyRangeType).Assembly]);

        offenders.Should().Contain(line => line.StartsWith(typeof(PlantedLazyRangeType).FullName!, StringComparison.Ordinal)
            && line.EndsWith("Range(TimeSpan)", StringComparison.Ordinal));
    }

    /// <summary>Lists every member of the authored types that carries a range with an operand other than int or double.</summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>One line per offending member.</returns>
    private static List<string> Offenders(IEnumerable<Assembly> assemblies)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return assemblies
            .SelectMany(Layers.AuthoredTypesOf)
            .SelectMany(type => type.GetMembers(all))
            .SelectMany(member => member.GetCustomAttributes<RangeAttribute>(inherit: false)
                .Where(range => range.OperandType != typeof(int) && range.OperandType != typeof(double))
                .Select(range => $"{member.DeclaringType!.FullName}.{member.Name} uses Range({range.OperandType.Name})"))
            .ToList();
    }
}
