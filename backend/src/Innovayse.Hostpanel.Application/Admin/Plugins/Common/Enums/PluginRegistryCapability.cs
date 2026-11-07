namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>A sensitive capability a plugin version declares it uses.</summary>
public enum PluginRegistryCapability
{
    /// <summary>Starts operating system processes.</summary>
    Process = 1,

    /// <summary>Opens raw network sockets.</summary>
    RawSockets = 2,

    /// <summary>Generates code at run time.</summary>
    ReflectionEmit = 3,

    /// <summary>Calls native code.</summary>
    NativeInterop = 4,

    /// <summary>Reads or writes files outside its own folder.</summary>
    FileSystem = 5,
}
