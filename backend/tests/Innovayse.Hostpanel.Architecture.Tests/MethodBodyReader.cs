using System.Reflection;
using System.Reflection.Emit;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Reads compiled method bodies for the methods they call, so a rule about what a handler does
/// (which sets it reads, whether it saves, whether it bulk-updates) is checked against the IL
/// rather than against a naming convention. Async state machines and lambda closures are nested
/// compiler-generated types, so every reader walks a type together with everything nested in it.
/// </summary>
internal static class MethodBodyReader
{
    /// <summary>Every method body binding: instance and static, any visibility, declared on the type itself.</summary>
    private const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Every opcode by its value, so raw IL can be walked operand by operand.</summary>
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    /// <summary>
    /// The methods a type calls, across its own methods and constructors and those of every nested
    /// type (async state machines, lambda closures).
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <returns>Each referenced method, once per call site.</returns>
    public static IEnumerable<MethodBase> CallsMadeBy(Type type)
    {
        foreach (var declaring in SelfAndNested(type))
        {
            var bodies = declaring.GetMethods(AllDeclared).Cast<MethodBase>().Concat(declaring.GetConstructors(AllDeclared));
            foreach (var method in bodies)
            {
                foreach (var called in CalledMethods(method))
                {
                    yield return called;
                }
            }
        }
    }

    /// <summary>A type and all its nested types, recursively.</summary>
    /// <param name="type">The outer type.</param>
    /// <returns>The type followed by every type nested in it.</returns>
    public static IEnumerable<Type> SelfAndNested(Type type)
    {
        yield return type;

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var inner in SelfAndNested(nested))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// Walks a method body's IL and resolves the target of every call, callvirt and ldftn, and every method named by
    /// ldtoken: a lambda that becomes an expression tree never calls the members it mentions, it loads their handles.
    /// </summary>
    /// <param name="method">The method to read.</param>
    /// <returns>The methods it references.</returns>
    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var position = 0;

        while (position < il.Length)
        {
            short value = il[position++];
            if (value == 0xFE)
            {
                value = (short)(0xFE00 | il[position++]);
            }

            var code = OpCodesByValue[value];
            if (code.OperandType is OperandType.InlineMethod or OperandType.InlineTok)
            {
                var token = BitConverter.ToInt32(il, position);
                var target = Resolve(method.Module, token, typeArguments, methodArguments);
                if (target is not null)
                {
                    yield return target;
                }
            }

            position += OperandSize(code, il, position);
        }
    }

    /// <summary>
    /// Resolves a method token, or nothing when the token cannot be bound in this context or names a type or a field
    /// (an ldtoken operand can be any of them).
    /// </summary>
    /// <param name="module">The module declaring the token.</param>
    /// <param name="token">The metadata token.</param>
    /// <param name="typeArguments">The declaring type's generic arguments, if any.</param>
    /// <param name="methodArguments">The method's generic arguments, if any.</param>
    /// <returns>The method, or <see langword="null"/>.</returns>
    private static MethodBase? Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
    {
        try
        {
            return module.ResolveMember(token, typeArguments, methodArguments) as MethodBase;
        }
        catch (ArgumentException)
        {
            // A token into an open generic the caller cannot bind; no rule here is about one.
            return null;
        }
    }

    /// <summary>The number of operand bytes following an opcode.</summary>
    /// <param name="code">The opcode.</param>
    /// <param name="il">The IL bytes, for the switch table length.</param>
    /// <param name="position">The operand's offset.</param>
    /// <returns>The operand size in bytes.</returns>
    private static int OperandSize(OpCode code, byte[] il, int position)
    {
        return code.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
            _ => 4,
        };
    }
}
