using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Finds the places that read the system clock directly: <c>DateTimeOffset.UtcNow</c>, <c>DateTimeOffset.Now</c>,
/// <c>DateTime.UtcNow</c>, <c>DateTime.Now</c> and <c>DateTime.Today</c>. The rule is a plain function of named sources, so a test
/// can run it on an in-memory planted source and on the real files. It reads the syntax tree, so a comment never counts as a read and
/// text that only looks like a comment or a read inside a string literal neither hides nor invents one. It checks the
/// <c>Type.Member</c> form only: a bare <c>UtcNow</c> brought in by <c>using static</c> and a read through an alias of the type
/// are out of scope, and <c>NoDirectClockTests</c> plants both to say so.
/// </summary>
internal static class DirectClockScan
{
    /// <summary>The types whose current-moment members are the system clock.</summary>
    private static readonly HashSet<string> ClockTypes = ["DateTimeOffset", "DateTime"];

    /// <summary>The members of <see cref="ClockTypes"/> that read the system clock.</summary>
    private static readonly HashSet<string> ClockMembers = ["UtcNow", "Now", "Today"];

    /// <summary>The sources that read the system clock outside a comment.</summary>
    /// <param name="sources">Pairs of a name for messages and the source text.</param>
    /// <returns>One <c>name: DateTimeOffset.UtcNow</c> line per read.</returns>
    public static IReadOnlyList<string> Offenders(IEnumerable<(string Name, string Text)> sources)
    {
        return sources
            .SelectMany(source => CSharpSyntaxTree.ParseText(source.Text).GetRoot()
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(IsClockRead)
                .Select(access => $"{source.Name}: {TypeName(access.Expression)}.{access.Name.Identifier.ValueText}"))
            .ToList();
    }

    /// <summary>Tells whether a member access is a read of one of the system clock's current-moment members.</summary>
    /// <param name="access">The member access.</param>
    /// <returns><see langword="true"/> for <c>DateTime.Now</c>, <c>System.DateTimeOffset.UtcNow</c> and the like.</returns>
    private static bool IsClockRead(MemberAccessExpressionSyntax access)
    {
        return ClockMembers.Contains(access.Name.Identifier.ValueText)
            && TypeName(access.Expression) is { } type
            && ClockTypes.Contains(type);
    }

    /// <summary>The simple name of the type an expression names, whether written alone or through its namespace.</summary>
    /// <param name="expression">The left side of a member access.</param>
    /// <returns>The type's simple name, or <see langword="null"/> when the expression is neither a name nor a qualified name.</returns>
    private static string? TypeName(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleNameSyntax name => name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };
    }
}
