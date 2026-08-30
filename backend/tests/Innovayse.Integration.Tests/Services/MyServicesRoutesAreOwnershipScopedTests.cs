namespace Innovayse.Integration.Tests.Services;

using System.Reflection;
using System.Runtime.CompilerServices;
using Innovayse.API.Services;
using Innovayse.Application.Services.Common;

/// <summary>
/// Fails when an action is added to <see cref="MyServicesController"/> that takes a service id off
/// the route and dispatches a message the ownership rule does not apply to.
/// <para>
/// This test is the point of the fix it belongs to. Five actions on this controller shipped
/// taking a service id from the URL and checking nothing — one of them handing back a working
/// control-panel login to whichever account the id named. Fixing five handlers stops those five;
/// it does nothing about the sixth, and the sixth is how this happened in the first place. So the
/// rule is asserted about the controller as a whole rather than about the actions that exist
/// today.
/// </para>
/// <para>
/// It reads IL rather than making HTTP calls, so it needs no database, no container and no
/// fixture: it asks which message types each action constructs and whether they carry
/// <see cref="ICallerScopedServiceMessage"/>. The companion assertion — that a marked message's
/// handler really does take <c>IServiceOwnership</c> — lives in
/// <c>CallerScopedServiceMessageTests</c> in <c>Innovayse.Application.Tests</c>, which is where
/// the handlers are visible.
/// </para>
/// </summary>
public sealed class MyServicesRoutesAreOwnershipScopedTests
{
    /// <summary>The <c>newobj</c> opcode, which is what a <c>new SomeCommand(...)</c> compiles to.</summary>
    private const byte NewObj = 0x73;

    /// <summary>
    /// Every action that takes a service id from the route must construct a caller-scoped message.
    /// </summary>
    [Fact]
    public void EveryActionTakingAServiceIdFromTheRoute_DispatchesACallerScopedMessage()
    {
        var actions = ActionsTakingARouteId();

        // A rename of the id parameter, or the controller moving, would otherwise leave this
        // green while asserting nothing at all.
        Assert.NotEmpty(actions);

        var unscoped = actions
            .Where(action => !ConstructedTypes(action).Any(
                t => typeof(ICallerScopedServiceMessage).IsAssignableFrom(t)))
            .Select(action => action.Name)
            .ToList();

        Assert.Empty(unscoped);
    }

    /// <summary>
    /// The actions on the controller that take an <see cref="int"/> route id — the ones that can
    /// be pointed at somebody else's service by editing the URL.
    /// </summary>
    /// <returns>The action methods to check.</returns>
    private static IReadOnlyList<MethodInfo> ActionsTakingARouteId() =>
        typeof(MyServicesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(int) && p.Name == "id"))
            .ToList();

    /// <summary>
    /// The types an action constructs, read out of its IL.
    /// </summary>
    /// <remarks>
    /// Every action here is <c>async</c>, so its own body is a few lines that start a state
    /// machine and the <c>newobj</c> instructions live in the generated <c>MoveNext</c>. This
    /// follows <see cref="AsyncStateMachineAttribute"/> to find it. Tokens that fail to resolve —
    /// a <c>0x73</c> byte that is part of some other instruction's operand rather than an opcode —
    /// are skipped: a stray token cannot make an unguarded action look guarded unless it happens
    /// to resolve to a marked message type, and it cannot hide a real one.
    /// </remarks>
    /// <param name="action">The action method.</param>
    /// <returns>The distinct types constructed while the action runs.</returns>
    private static IReadOnlyList<Type> ConstructedTypes(MethodInfo action)
    {
        var body = BodyOf(action);
        var il = body?.GetILAsByteArray();
        if (il is null)
        {
            return [];
        }

        var module = action.Module;
        var found = new List<Type>();

        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != NewObj)
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, i + 1);

            try
            {
                var declaring = module.ResolveMethod(token)?.DeclaringType;
                if (declaring is not null)
                {
                    found.Add(declaring);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or BadImageFormatException or InvalidOperationException)
            {
                // Not a real metadata token; this byte was operand data, not an opcode.
            }
        }

        return found.Distinct().ToList();
    }

    /// <summary>
    /// The method body that actually runs an action: the compiler-generated <c>MoveNext</c> for an
    /// <c>async</c> method, or the method itself otherwise.
    /// </summary>
    /// <param name="action">The action method.</param>
    /// <returns>The body to read, or <see langword="null"/> when there is none.</returns>
    private static MethodBody? BodyOf(MethodInfo action)
    {
        var stateMachine = action.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;

        var moveNext = stateMachine?.GetMethod(
            "MoveNext",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        return (moveNext ?? action).GetMethodBody();
    }
}
