using System.Collections.Concurrent;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Results;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// What a <see cref="FakeLoginlessPanelPlugin"/> answers and what it received. The host builds a new plugin instance for every call, so
/// the script lives outside it, found by the name of the server the call is for: a test names its server uniquely and registers
/// a script under that name with <see cref="Register"/>.
/// </summary>
public sealed class FakePanelScript
{
    /// <summary>The scripts of the running tests, by server name.</summary>
    private static readonly ConcurrentDictionary<string, FakePanelScript> Scripts = new(StringComparer.Ordinal);

    /// <summary>Every context the plugin received, in order, whichever operation it was.</summary>
    private readonly ConcurrentQueue<object> _received = new();

    /// <summary>Gets what the create operation answers.</summary>
    public PanelCreateResult Create { get; set; } = PanelCreateResult.Created("acct-1");

    /// <summary>Gets what suspend, unsuspend, terminate, change package and change password answer.</summary>
    public PanelActionResult Action { get; set; } = PanelActionResult.Done();

    /// <summary>Gets what the sign in link operation answers.</summary>
    public PanelSsoResult Sso { get; set; } = PanelSsoResult.Issued("https://panel.example.test/login?token=abc");

    /// <summary>Gets what the usage operation answers.</summary>
    public PanelUsageResult Usage { get; set; } = PanelUsageResult.Read(
        new PanelUsage(10L * 1024 * 1024, 100L * 1024 * 1024, 5L * 1024 * 1024, null, DateTimeOffset.UnixEpoch));

    /// <summary>Gets what the test connection operation answers.</summary>
    public PanelConnectionTestResult Test { get; set; } = PanelConnectionTestResult.Connected("1.2.3", 4);

    /// <summary>Gets the pages the list accounts operation answers, by the cursor it was given (<see langword="null"/> for the first).</summary>
    public Func<string?, PanelAccountListResult> List { get; set; } = _ => PanelAccountListResult.Page(
        [new PanelAccount("acct-1", "one.example.test", PanelAccountStatus.Active, "basic", null)], null);

    /// <summary>Gets what the login details operation answers.</summary>
    public ValueResult<PanelLoginDetails, PanelRefusalReason> Login { get; set; } = ValueResult<PanelLoginDetails, PanelRefusalReason>.Done(
        new PanelLoginDetails("acct-1", "panel-password", "one.example.test", "203.0.113.9", "https://panel.example.test:2087"));

    /// <summary>Gets or sets what every operation throws instead of answering, when set.</summary>
    public Exception? Throw { get; set; }

    /// <summary>Gets the type of the transport the plugin was handed, noted by the first call it made.</summary>
    public Type? TransportType { get; private set; }

    /// <summary>Gets every context the plugin received, in order.</summary>
    public IReadOnlyCollection<object> Received => _received;

    /// <summary>Registers a script under a server name, so a plugin call for that server finds it.</summary>
    /// <param name="serverName">The test's unique server name.</param>
    /// <returns>The new script, to be filled in by the test.</returns>
    public static FakePanelScript Register(string serverName)
    {
        var script = new FakePanelScript();
        Scripts[serverName] = script;
        return script;
    }

    /// <summary>Finds the script of the server a plugin call is for.</summary>
    /// <param name="server">The connection the plugin received.</param>
    /// <returns>The script registered for the server's name.</returns>
    internal static FakePanelScript For(PanelServerConnection server)
    {
        return Scripts[server.Name];
    }

    /// <summary>Records a received context and throws the scripted exception, if any.</summary>
    /// <param name="context">The context the plugin received.</param>
    /// <param name="transport">The transport the plugin was handed.</param>
    internal void Note(object context, IPanelTransport transport)
    {
        TransportType ??= transport.GetType();
        _received.Enqueue(context);
        if (Throw is not null)
        {
            throw Throw;
        }
    }
}
