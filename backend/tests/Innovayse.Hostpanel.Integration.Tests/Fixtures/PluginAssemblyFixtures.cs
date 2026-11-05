using System.Reflection;
using System.Reflection.Emit;
using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builds tiny managed assemblies whose only content is a reference to the SDK at a chosen version, so a test can
/// stand in for a plugin built against an older or the current SDK without owning a second copy of the SDK.
/// </summary>
internal static class PluginAssemblyFixtures
{
    /// <summary>
    /// Builds an assembly with one type that derives from a type of an assembly named like the SDK at
    /// <paramref name="sdkVersion"/>, which makes the assembly reference the SDK at exactly that version.
    /// </summary>
    /// <param name="sdkVersion">The SDK version the assembly is "built against".</param>
    /// <returns>The raw bytes of the assembly file.</returns>
    public static byte[] BuiltAgainstSdk(Version sdkVersion)
    {
        return BuiltAgainstAssembly(typeof(PluginManifest).Assembly.GetName().Name!, sdkVersion);
    }

    /// <summary>
    /// Builds an assembly with one type (<c>Fixture.PluginType</c>) that derives from a type of an assembly named
    /// <paramref name="dependencyName"/> at <paramref name="dependencyVersion"/>. When no such assembly can be found
    /// the type cannot be loaded, which is how a plugin built against an SDK the host does not carry fails.
    /// </summary>
    /// <param name="dependencyName">The simple name of the assembly the plugin type derives from.</param>
    /// <param name="dependencyVersion">The version of that assembly the plugin is "built against".</param>
    /// <param name="pluginAssemblyName">
    /// The name of the plugin assembly. A test that loads it passes a name of its own: the default context keeps one
    /// assembly per name for the life of the process, and another test asserts the default name was never loaded.
    /// </param>
    /// <returns>The raw bytes of the assembly file.</returns>
    public static byte[] BuiltAgainstAssembly(string dependencyName, Version dependencyVersion, string pluginAssemblyName = "Fixture.Plugin")
    {
        var dependency = new PersistedAssemblyBuilder(new AssemblyName(dependencyName) { Version = dependencyVersion }, typeof(object).Assembly);
        var dependencyType = dependency.DefineDynamicModule(dependencyName).DefineType("Fixture.SdkType", TypeAttributes.Public | TypeAttributes.Class);
        dependencyType.CreateType();

        var plugin = new PersistedAssemblyBuilder(new AssemblyName(pluginAssemblyName), typeof(object).Assembly);
        var pluginType = plugin.DefineDynamicModule(pluginAssemblyName)
            .DefineType("Fixture.PluginType", TypeAttributes.Public | TypeAttributes.Class, dependencyType);
        pluginType.CreateType();

        return Save(plugin);
    }

    /// <summary>
    /// Builds an assembly with one app plugin type that implements <see cref="IPluginEndpoints"/> against the real
    /// SDK. Its name is the caller's, so each test can give the loader an assembly no other test has loaded.
    /// </summary>
    /// <param name="assemblyName">The unique assembly name.</param>
    /// <param name="typeName">The full name of the plugin type, as a manifest's <c>entryPoint</c> spells it.</param>
    /// <returns>The raw bytes of the assembly file.</returns>
    public static byte[] AppPlugin(string assemblyName, string typeName)
    {
        var plugin = new PersistedAssemblyBuilder(new AssemblyName(assemblyName), typeof(object).Assembly);
        var type = plugin.DefineDynamicModule(assemblyName).DefineType(
            typeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(object), [typeof(IPluginEndpoints)]);
        type.DefineDefaultConstructor(MethodAttributes.Public);

        var handle = type.DefineMethod(
            nameof(IPluginEndpoints.HandleAsync),
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
            typeof(Task<PluginReply>),
            [typeof(PluginCall), typeof(CancellationToken)]);
        var il = handle.GetILGenerator();
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(handle, typeof(IPluginEndpoints).GetMethod(nameof(IPluginEndpoints.HandleAsync))!);
        type.CreateType();

        return Save(plugin);
    }

    /// <summary>
    /// Builds an assembly with one public type that implements no SDK contract at all, so a manifest that names it as the
    /// entry point of a plugin that declares a capability is declaring something the class does not do.
    /// </summary>
    /// <param name="assemblyName">The unique assembly name.</param>
    /// <param name="typeName">The full name of the plugin type, as a manifest's <c>entryPoint</c> spells it.</param>
    /// <returns>The raw bytes of the assembly file.</returns>
    public static byte[] PlainPlugin(string assemblyName, string typeName)
    {
        var plugin = new PersistedAssemblyBuilder(new AssemblyName(assemblyName), typeof(object).Assembly);
        var type = plugin.DefineDynamicModule(assemblyName).DefineType(
            typeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        type.CreateType();

        return Save(plugin);
    }

    /// <summary>Writes a built assembly to memory.</summary>
    /// <param name="assembly">The assembly builder.</param>
    /// <returns>The raw bytes of the assembly file.</returns>
    private static byte[] Save(PersistedAssemblyBuilder assembly)
    {
        using var stream = new MemoryStream();
        assembly.Save(stream);
        return stream.ToArray();
    }
}
