using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// <see cref="RangeAttribute"/> converts bounds given as strings lazily and not thread-safely, so
/// <c>[Range(typeof(TimeSpan), ...)]</c> throws <see cref="InvalidCastException"/> when several hosts validate
/// at once (issue 756). Options with such bounds are checked by an options validator instead.
/// </summary>
public sealed class NoLazyRangeTests
{
    /// <summary>No property or field in the authored assemblies uses a range whose operand is not int or double.</summary>
    [Fact]
    public void RangeAttribute_UsesOnlyIntOrDoubleOperands()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var assemblies = new[] { Layers.Domain, Layers.Shared, Layers.Application, Layers.Infrastructure, Layers.Api };

        var offenders = assemblies
            .SelectMany(Layers.AuthoredTypesOf)
            .SelectMany(type => type.GetMembers(all))
            .SelectMany(member => member.GetCustomAttributes<RangeAttribute>(inherit: false)
                .Where(range => range.OperandType != typeof(int) && range.OperandType != typeof(double))
                .Select(range => $"{member.DeclaringType!.FullName}.{member.Name} uses Range({range.OperandType.Name})"))
            .ToList();

        offenders.Should().BeEmpty("a typed Range converts lazily and is not thread-safe; validate the bound in an IValidateOptions instead");
    }
}
