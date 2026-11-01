using System.Reflection;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builds a <see cref="PluginReply"/> without its factory methods, which refuse a status outside the contract. It stands
/// in for a reply the factories did not vouch for (a plugin compiled against another SDK, or built by reflection), so a
/// test can prove the host checks the status again instead of trusting the type.
/// </summary>
public static class ForgedPluginReply
{
    /// <summary>Builds a reply of any kind with any status and parts.</summary>
    /// <param name="kind">The shape of the reply.</param>
    /// <param name="status">The status, whether or not the contract allows it for that shape.</param>
    /// <param name="body">The JSON body, or <see langword="null"/>.</param>
    /// <param name="code">The problem code, or <see langword="null"/>.</param>
    /// <param name="detail">The problem sentence, or <see langword="null"/>.</param>
    /// <returns>The reply.</returns>
    public static PluginReply Create(PluginReplyKind kind, int status, object? body = null, string? code = null, string? detail = null)
    {
        var reply = (PluginReply)Activator.CreateInstance(
            typeof(PluginReply), BindingFlags.Instance | BindingFlags.NonPublic, null, [kind, status], null)!;

        Set(reply, nameof(PluginReply.Body), body);
        Set(reply, nameof(PluginReply.Code), code);
        Set(reply, nameof(PluginReply.Detail), detail);
        return reply;
    }

    /// <summary>Sets a property whose setter is private.</summary>
    /// <param name="reply">The reply.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    private static void Set(PluginReply reply, string name, object? value)
    {
        typeof(PluginReply).GetProperty(name)!.GetSetMethod(nonPublic: true)!.Invoke(reply, [value]);
    }
}
