using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// A call to a control panel's API is dialled at the server's API endpoint host (its API address, or its host name when it has
/// none), never at the plain host name. The plain host name builds only what a client sees, such as the user panel link, and a
/// call that used it would ignore the API address an operator set for a panel behind a VPN. The rule is a plain function of
/// named sources, so a test can run it on an in-memory planted source and on the real files.
/// </summary>
public sealed partial class ApiEndpointHostTests
{
    /// <summary>The projects whose code dials a panel.</summary>
    private static readonly string[] DialingProjects = ["Innovayse.Hostpanel.Application", "Innovayse.Hostpanel.Infrastructure"];

    /// <summary>The solution directory.</summary>
    private static string BackendRoot => UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory);

    /// <summary>
    /// The lines of a source that build a panel API address or connection from a server's plain host name. A line that names the
    /// user panel port builds a link a client opens, which is meant to use the host name, so it is not an API call.
    /// </summary>
    /// <param name="sources">Pairs of a name for messages and the source text.</param>
    /// <returns>One <c>name:line</c> entry per offending line.</returns>
    internal static IReadOnlyList<string> Offenders(IEnumerable<(string Name, string Text)> sources)
    {
        return sources
            .SelectMany(source => source.Text.Split('\n')
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(entry => PlainHostnameDial().IsMatch(entry.Line) && !entry.Line.Contains("UserPort", StringComparison.Ordinal) && !entry.Line.Contains("UserPanelPort", StringComparison.Ordinal))
                .Select(entry => $"{source.Name}:{entry.Number}"))
            .ToList();
    }

    /// <summary>No panel API call in the Application or Infrastructure source is built from a server's plain host name.</summary>
    [Fact]
    public void PanelApiCalls_AreDialledAtTheApiEndpointHost()
    {
        var src = Path.Combine(BackendRoot, "src");
        var sources = DialingProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(src, project), "*.cs", SearchOption.AllDirectories))
            .Select(path => (Name: Path.GetRelativePath(src, path).Replace('\\', '/'), Path: path))
            .Where(file => !file.Name.Contains("/obj/", StringComparison.Ordinal) && !file.Name.Contains("/bin/", StringComparison.Ordinal))
            .Select(file => (file.Name, Text: File.ReadAllText(file.Path)))
            .ToList();

        Offenders(sources).Should().BeEmpty("dial server.ApiEndpointHost, and keep the plain host name for the links a client sees");
    }

    /// <summary>The rule finds a panel API address built from the host name and leaves the API endpoint host and the user panel link alone.</summary>
    [Fact]
    public void Offenders_FindsAPlainHostnameDial_AndSpareTheApiEndpointHostAndTheUserPanelLink()
    {
        var planted = new (string Name, string Text)[]
        {
            ("Bad.cs", "var c = new WhmConnection(HostingPanelAddress.BaseUrl(server.Hostname, options.CPanelWhmPort), u, t);"),
            ("BadApi.cs", "api = CpanelWhmApi.Create(server.Hostname, server.Username, token, factory);"),
            ("BadHash.cs", "api = CpanelWhmApi.CreateWithHash(server.Hostname, server.Username, hash, factory);"),
            ("BadConnection.cs", "new(server.Hostname, IHostingPanelApiClient.DefaultApiPort, apiKey);"),
            ("Good.cs", "var c = new WhmConnection(HostingPanelAddress.BaseUrl(server.ApiEndpointHost, options.CPanelWhmPort), u, t);"),
            ("Link.cs", "var panelUrl = HostingPanelAddress.BaseUrl(server.Hostname, options.CPanelUserPort);"),
        };

        Offenders(planted).Should().Equal("Bad.cs:1", "BadApi.cs:1", "BadHash.cs:1", "BadConnection.cs:1");
    }

    /// <summary>A panel address or connection whose first argument is some variable's plain <c>Hostname</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"(HostingPanelAddress\.BaseUrl|CpanelWhmApi\.Create(WithHash)?|new WhmConnection|new HostingPanelConnection|\bnew)\(\s*\w+\.Hostname\b", RegexOptions.CultureInvariant)]
    private static partial Regex PlainHostnameDial();
}
