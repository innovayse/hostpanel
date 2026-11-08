using System.Net;
using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Registry;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistryClient"/> against a scripted HTTP handler: the wire shapes it reads, that every request and
/// redirect stays on the registry's host, the size and time limits, and that a failure becomes a
/// <c>PLUGIN_REGISTRY_*</c> code with nothing of the upstream reply in it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, which supplies the real request signer the client is built with.</param>
public sealed class PluginRegistryClientTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>The scopes the signers of the clients built here were resolved in; disposed with the test.</summary>
    private readonly List<IServiceScope> _scopes = [];

    /// <summary>An upload folder of this test's own, so the staging folder the downloads use is isolated and checked.</summary>
    private readonly string _installPath = Path.Combine(Path.GetTempPath(), $"registry-client-{Guid.NewGuid():N}");

    /// <summary>Deletes the test's upload folder.</summary>
    public void Dispose()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        if (Directory.Exists(_installPath))
        {
            Directory.Delete(_installPath, recursive: true);
        }
    }

    /// <summary>The files left in the staging folder.</summary>
    /// <returns>The file paths; none when the folder was never created.</returns>
    private string[] StagedFiles()
    {
        var staging = Path.Combine(new PluginOptions { InstallPath = _installPath }.InstallRoot, ".staging");
        return Directory.Exists(staging) ? Directory.GetFiles(staging) : [];
    }

    /// <summary>A registry address for the tests.</summary>
    private const string BaseUrl = "https://registry.example.com/api/";

    /// <summary>A valid published checksum.</summary>
    private static readonly string Sha = new('b', 64);

    /// <summary>Builds a client over a handler.</summary>
    /// <param name="handler">The scripted handler.</param>
    /// <param name="enabled">Whether the registry is switched on.</param>
    /// <param name="maxBytes">The download limit.</param>
    /// <param name="timeout">The time limit; 10 seconds when <see langword="null"/>.</param>
    /// <returns>The client.</returns>
    private PluginRegistryClient Client(HttpMessageHandler handler, bool enabled = true, long maxBytes = 1024, TimeSpan? timeout = null)
    {
        var options = Options.Create(new PluginRegistryOptions
        {
            Enabled = enabled,
            BaseUrl = BaseUrl,
            MaxDownloadBytes = maxBytes,
            Timeout = timeout ?? TimeSpan.FromSeconds(10),
        });
        var scope = factory.Services.CreateScope();
        _scopes.Add(scope);
        return new PluginRegistryClient(
            new HttpClient(handler),
            options,
            Options.Create(new PluginOptions { InstallPath = _installPath }),
            scope.ServiceProvider.GetRequiredService<IRegistryRequestSigner>(),
            NullLogger<PluginRegistryClient>.Instance);
    }

    /// <summary>One valid plugin as the registry sends it: <c>pluginId</c> and <c>publisherName</c>, no SDK version.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="extra">Further members, starting with a comma; empty for none.</param>
    /// <returns>The JSON object text.</returns>
    private static string EntryJson(string id, string extra = "")
    {
        return $$"""{"pluginId":"{{id}}","name":"Name","description":"d","publisherName":"a","category":"Payment","latestVersion":"1.2.3","verified":true,"pricing":"Paid"{{extra}}}""";
    }

    /// <summary>One version of a plugin as the registry sends it in a list or in a plugin page.</summary>
    /// <param name="version">The version number.</param>
    /// <param name="extra">Further members, starting with a comma; empty for none.</param>
    /// <returns>The JSON object text.</returns>
    private static string VersionJson(string version, string extra = "")
    {
        return $$"""{"version":"{{version}}","sdkVersion":"3.1.0","sizeBytes":2048,"publishedAt":"2026-01-02T03:04:05Z","status":"Published","statusReason":null,"replacementVersion":null{{extra}}}""";
    }

    /// <summary>A switched-off registry answers <c>PLUGIN_REGISTRY_DISABLED</c> and sends nothing.</summary>
    [Fact]
    public async Task ADisabledRegistry_SendsNothingAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("{}");

        var result = await Client(handler, enabled: false).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A listing is read into entries and the filters are put in the address.</summary>
    [Fact]
    public async Task Search_ReadsTheListingAndSendsTheFiltersAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"items":[{{EntryJson("acme-pay")}}],"total":41}""");

        var result = await Client(handler).SearchAsync(new RegistryPluginSearch("a b&c", PluginRegistryCategory.Payment, 2, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(41);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(20);
        var entry = result.Value.Items.Single();
        entry.Should().Be(new RegistryPluginDto("acme-pay", "Name", "a", "d", null, PluginRegistryCategory.Payment, "1.2.3", string.Empty, true, PluginRegistryPricing.Paid));
        handler.Requests.Single().AbsoluteUri.Should().Be("https://registry.example.com/api/plugins?q=a%20b%26c&category=Payment&page=2&pageSize=20");
    }

    /// <summary>An unknown category reads as Other; an icon is kept only when it is on the registry's host.</summary>
    [Fact]
    public async Task Search_NormalisesTheCategoryAndDropsAForeignIconAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""
            {"items":[
              {{EntryJson("one").Replace("Payment", "Surprise")}},
              {{EntryJson("two", ",\"iconUrl\":\"https://evil.example.net/i.png\"")}},
              {{EntryJson("three", ",\"iconUrl\":\"/icons/three.svg\"")}}],"total":3}
            """);

        var result = await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);

        var items = result.Value!.Items;
        items[0].Category.Should().Be(PluginRegistryCategory.Other);
        items[1].IconUrl.Should().BeNull();
        items[2].IconUrl.Should().Be("https://registry.example.com/icons/three.svg");
    }

    /// <summary>An entry that is not a plugin (bad id, no pricing, no name) fails the whole reply as invalid.</summary>
    /// <param name="entry">The entry JSON.</param>
    [Theory]
    [InlineData("""{"id":"Bad Id","name":"n","latestVersion":"1.0.0","pricing":"Free"}""")]
    [InlineData("""{"id":"ok-id","name":"n","latestVersion":"1.0.0","pricing":"Gold"}""")]
    [InlineData("""{"id":"ok-id","name":"","latestVersion":"1.0.0","pricing":"Free"}""")]
    public async Task Search_AnInvalidEntry_FailsTheReplyAsync(string entry)
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"items":[{{entry}}],"total":1}""");

        var result = await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
    }

    /// <summary>The registry's own member names are read: <c>pluginId</c> and <c>publisherName</c>, and the short forms too.</summary>
    [Fact]
    public async Task Search_ReadsBothNameFormsAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("""
            {"items":[
              {"pluginId":"long-form","name":"A","publisherName":"Publisher","latestVersion":"1.0.0","pricing":"Free"},
              {"id":"short-form","name":"B","author":"Writer","latestVersion":"1.0.0","pricing":"Free"}],"total":2}
            """);

        var items = (await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None)).Value!.Items;

        items.Select(i => (i.Id, i.Author)).Should().Equal(("long-form", "Publisher"), ("short-form", "Writer"));
    }

    /// <summary>An entry whose latest version is null, missing or not a version is left out and the rest of the page is kept.</summary>
    [Fact]
    public async Task Search_SkipsAnEntryWithoutAUsableLatestVersionAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("""
            {"items":[
              {"pluginId":"no-release","name":"A","latestVersion":null,"pricing":"Free"},
              {"pluginId":"missing-field","name":"B","pricing":"Free"},
              {"pluginId":"bad-number","name":"C","latestVersion":"latest","pricing":"Free"},
              {"pluginId":"kept","name":"D","latestVersion":"2.0.0","pricing":"Free"}],"total":4}
            """);

        var result = await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Select(i => i.Id).Should().Equal("kept");
    }

    /// <summary>The newest-version lookup posts the ids as JSON and reads a bare array of version details.</summary>
    [Fact]
    public async Task GetLatest_PostsTheIdsAndReadsABareArrayAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("""
            [{"pluginId":"acme-pay","version":"1.2.3","downloadUrl":"/x.zip","sha256":"abc","verified":true,"sdkVersion":"3.1.0","requiresLicence":true},
             {"pluginId":"free-one","version":"0.9.0","downloadUrl":"/y.zip","sha256":"abc","verified":false,"sdkVersion":"3.0.0"},
             {"pluginId":"odd-one","version":"nonsense","downloadUrl":"/z.zip","sha256":"abc","verified":false}]
            """);

        var result = await Client(handler).GetLatestAsync(["acme-pay", "other"], CancellationToken.None);

        result.Value!.Select(p => (p.Id, p.LatestVersion, p.SdkVersion, p.Verified, p.Pricing)).Should().Equal(
            ("acme-pay", "1.2.3", "3.1.0", true, PluginRegistryPricing.Paid),
            ("free-one", "0.9.0", "3.0.0", false, PluginRegistryPricing.Free));
        handler.Requests.Single().AbsoluteUri.Should().Be("https://registry.example.com/api/plugins/latest");
        handler.Bodies.Single().Should().Be("""{"ids":["acme-pay","other"]}""");
    }

    /// <summary>A release is resolved, a relative package address is made absolute on the registry's host, the checksum is lower-cased.</summary>
    [Fact]
    public async Task GetRelease_ResolvesARelativePackageAddressAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"pluginId":"acme-pay","version":"1.2.3","downloadUrl":"/files/acme.zip","sha256":"{{Sha.ToUpperInvariant()}}","verified":true}""");

        var result = await Client(handler).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None);

        result.Value.Should().Be(new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/acme.zip"), Sha, true));
        handler.Requests.Single().AbsoluteUri.Should().Be("https://registry.example.com/api/plugins/acme-pay/versions/1.2.3");
    }

    /// <summary>A package address on another host, another scheme or another port is refused; a missing or malformed checksum is invalid.</summary>
    /// <param name="downloadUrl">The package address.</param>
    /// <param name="sha">The checksum.</param>
    /// <param name="code">The expected error code.</param>
    [Theory]
    [InlineData("https://evil.example.net/a.zip", "SHA", HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED)]
    [InlineData("http://registry.example.com/a.zip", "SHA", HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED)]
    [InlineData("https://registry.example.com:8443/a.zip", "SHA", HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED)]
    [InlineData("https://user@registry.example.com/a.zip", "SHA", HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED)]
    [InlineData("/a.zip", "not-hex", HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID)]
    [InlineData("", "SHA", HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID)]
    public async Task GetRelease_RefusesAnUnsafeReleaseAsync(string downloadUrl, string sha, string code)
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"downloadUrl":"{{downloadUrl}}","sha256":"{{(sha == "SHA" ? Sha : sha)}}","verified":true}""");

        var result = await Client(handler).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None);

        result.ErrorCode.Should().Be(code);
    }

    /// <summary>A 404 for a version is <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task GetRelease_A404_IsNotFoundAsync()
    {
        var result = await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.NotFound)).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "9.9.9"), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
    }

    /// <summary>A package is downloaded into a seekable stream at position zero.</summary>
    [Fact]
    public async Task Download_ReturnsTheBytesAsync()
    {
        var bytes = Encoding.UTF8.GetBytes("package");
        var handler = new ScriptedHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);

        var result = await Client(handler).DownloadAsync(release, CancellationToken.None);

        await using (var stream = result.Value!)
        {
            stream.Should().BeOfType<FileStream>("a package is never held in memory");
            stream.CanSeek.Should().BeTrue();
            stream.Position.Should().Be(0);
            stream.Length.Should().Be(bytes.Length);
            StagedFiles().Should().ContainSingle();
        }

        StagedFiles().Should().BeEmpty("disposing the stream deletes the temporary file");
    }

    /// <summary>A package address outside the registry's origin is refused without a request.</summary>
    [Fact]
    public async Task Download_FromAnotherHost_IsRefusedWithoutARequestAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("x");
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://evil.example.net/a.zip"), Sha, true);

        var result = await Client(handler).DownloadAsync(release, CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A package over the limit is refused whether or not the reply declares its length.</summary>
    /// <param name="declareLength">Whether the reply carries a <c>Content-Length</c>.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Download_OverTheLimit_IsRefusedAsync(bool declareLength)
    {
        var bytes = new byte[2048];
        var handler = new ScriptedHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = declareLength ? new ByteArrayContent(bytes) : new UnknownLengthContent(bytes),
        }));
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);

        var result = await Client(handler, maxBytes: 1024).DownloadAsync(release, CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        StagedFiles().Should().BeEmpty("a refused download leaves no file behind");
    }

    /// <summary>A redirect inside the registry's host is followed; a redirect to another host is refused and that host is never contacted.</summary>
    [Fact]
    public async Task Redirects_AreFollowedOnlyInsideTheRegistryHostAsync()
    {
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);
        var inside = new ScriptedHttpMessageHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/files/a.zip"
            ? Redirect("https://registry.example.com/cdn/a.zip")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }));
        var outside = new ScriptedHttpMessageHandler((_, _) => Task.FromResult(Redirect("https://evil.example.net/a.zip")));

        var followed = await Client(inside).DownloadAsync(release, CancellationToken.None);
        var refused = await Client(outside).DownloadAsync(release, CancellationToken.None);

        followed.Value!.Length.Should().Be(3);
        inside.Requests.Select(r => r.AbsolutePath).Should().Equal("/files/a.zip", "/cdn/a.zip");
        refused.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        outside.Requests.Should().ContainSingle().Which.Host.Should().Be("registry.example.com");
    }

    /// <summary>A redirect loop ends in <c>PLUGIN_REGISTRY_UNAVAILABLE</c> after a few hops.</summary>
    [Fact]
    public async Task ARedirectLoop_StopsAsync()
    {
        var handler = new ScriptedHttpMessageHandler((_, _) => Task.FromResult(Redirect("https://registry.example.com/loop")));
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/loop"), Sha, true);

        var result = await Client(handler).DownloadAsync(release, CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        handler.Requests.Count.Should().BeLessThan(10);
    }

    /// <summary>Each upstream failure has its code, and the result carries no text from the failure or the reply.</summary>
    [Fact]
    public async Task UpstreamFailures_BecomeCodesWithoutDetailAsync()
    {
        var search = new RegistryPluginSearch(null, null, 1, 20);
        var serverError = ScriptedHttpMessageHandler.Json("secret upstream text", HttpStatusCode.InternalServerError);
        var network = new ScriptedHttpMessageHandler((_, _) => throw new HttpRequestException("secret host registry.internal"));
        var notJson = ScriptedHttpMessageHandler.Json("<html>secret</html>");
        var tooBig = new ScriptedHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(new byte[1_100_000]) }));

        var results = new[]
        {
            await Client(serverError).SearchAsync(search, CancellationToken.None),
            await Client(network).SearchAsync(search, CancellationToken.None),
            await Client(notJson).SearchAsync(search, CancellationToken.None),
            await Client(tooBig).SearchAsync(search, CancellationToken.None),
        };

        results.Select(r => r.ErrorCode).Should().Equal(
            HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE,
            HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE,
            HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID,
            HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
        results.Should().OnlyContain(r => r.Error == null && r.Errors == null);
    }

    /// <summary>A registry that does not answer within the time limit is <c>PLUGIN_REGISTRY_TIMED_OUT</c>; the caller's own cancellation is not.</summary>
    [Fact]
    public async Task ASlowRegistry_TimesOutAsync()
    {
        var handler = new ScriptedHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var search = new RegistryPluginSearch(null, null, 1, 20);

        var result = await Client(handler, timeout: TimeSpan.FromMilliseconds(100)).SearchAsync(search, CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_TIMED_OUT);
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var act = () => Client(handler).SearchAsync(search, cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>A 404 means "not listed" for a release lookup only; for a listing, the newest-version lookup and a download it is an unavailable registry.</summary>
    [Fact]
    public async Task A404_IsNotFoundOnlyForAReleaseAsync()
    {
        var notFound = () => ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.NotFound);
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);

        (await Client(notFound()).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        (await Client(notFound()).GetLatestAsync(["acme-pay"], CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        (await Client(notFound()).DownloadAsync(release, CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        StagedFiles().Should().BeEmpty();
    }

    /// <summary>A file system fault and a malformed address become registry codes instead of an unhandled exception.</summary>
    [Fact]
    public async Task IoAndUriFaults_BecomeCodesAsync()
    {
        var search = new RegistryPluginSearch(null, null, 1, 20);
        var io = new ScriptedHttpMessageHandler((_, _) => throw new IOException("disk detail"));
        var badUri = new ScriptedHttpMessageHandler((_, _) => throw new UriFormatException("uri detail"));

        var ioResult = await Client(io).SearchAsync(search, CancellationToken.None);
        var uriResult = await Client(badUri).SearchAsync(search, CancellationToken.None);

        ioResult.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        uriResult.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
        ioResult.Error.Should().BeNull();
    }

    /// <summary>Each request sets its own <c>Accept</c> header: JSON for a call, a package type for a download.</summary>
    [Fact]
    public async Task EachRequest_SetsItsAcceptHeaderAsync()
    {
        var accepts = new List<string>();
        var handler = new ScriptedHttpMessageHandler((request, _) =>
        {
            accepts.Add(request.Headers.Accept.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""{"items":[],"total":0}""")) });
        });
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);

        await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);
        await using var download = (await Client(handler).DownloadAsync(release, CancellationToken.None)).Value!;

        accepts.Should().Equal("application/json", "application/zip, application/octet-stream");
    }

    /// <summary>The registry's text is cleaned of control characters, trimmed and capped before it reaches the admin panel.</summary>
    [Fact]
    public async Task RegistryText_IsCleanedAndCappedAsync()
    {
        var long300 = new string('n', 300);
        var long3000 = new string('d', 3000);
        var handler = ScriptedHttpMessageHandler.Json(
            $$"""{"items":[{"id":"acme-pay","name":"  Pay\nPal\u0007  ","description":"{{long3000}}","author":"{{long300}}","category":"Payment","latestVersion":"1.0.0","sdkVersion":"1.0","verified":true,"pricing":"Free"}],"total":1}""");

        var plugin = (await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None)).Value!.Items.Single();

        plugin.Name.Should().Be("Pay Pal");
        plugin.Description.Should().HaveLength(2000);
        plugin.Author.Should().HaveLength(200);
    }

    /// <summary>A name that is blank once cleaned is not a plugin.</summary>
    [Fact]
    public async Task ABlankNameAfterCleaning_FailsTheReplyAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json(
            """{"items":[{"id":"acme-pay","name":"\u0007 \n","latestVersion":"1.0.0","pricing":"Free"}],"total":1}""");

        var result = await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
    }

    /// <summary>A reply that is not a bare array (the old listing shape) is invalid for the newest-version lookup.</summary>
    [Fact]
    public async Task GetLatest_AnObjectReply_IsInvalidAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"items":[{{EntryJson("acme-pay")}}],"total":1}""");

        var result = await Client(handler).GetLatestAsync(["acme-pay"], CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
    }

    /// <summary>A scripted handler that records the licence header of every request and answers each with a status.</summary>
    /// <param name="keys">Receives the header value of each request, <see langword="null"/> when it was not sent.</param>
    /// <param name="status">The status to answer with.</param>
    /// <param name="body">The body to answer with.</param>
    /// <returns>The handler.</returns>
    private static ScriptedHttpMessageHandler KeyRecorder(List<string?> keys, HttpStatusCode status, string body)
    {
        return new ScriptedHttpMessageHandler((request, _) =>
        {
            keys.Add(request.Headers.TryGetValues("X-Licence-Key", out var values) ? string.Join(",", values) : null);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        });
    }

    /// <summary>The licence key is sent as <c>X-Licence-Key</c> on a release lookup and only when one is given.</summary>
    [Fact]
    public async Task GetRelease_SendsTheLicenceKeyOnlyWhenGivenAsync()
    {
        var keys = new List<string?>();
        var handler = KeyRecorder(keys, HttpStatusCode.OK, $$"""{"downloadUrl":"/a.zip","sha256":"{{Sha}}","verified":true}""");

        await Client(handler).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None, "key-123");
        await Client(handler).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None);
        await Client(handler).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None, "   ");

        keys.Should().Equal("key-123", null, null);
    }

    /// <summary>The licence key is sent on a download, including the hops of a redirect inside the registry's origin.</summary>
    [Fact]
    public async Task Download_SendsTheLicenceKeyOnEveryHopAsync()
    {
        var keys = new List<string?>();
        var handler = new ScriptedHttpMessageHandler((request, _) =>
        {
            keys.Add(request.Headers.TryGetValues("X-Licence-Key", out var values) ? string.Join(",", values) : null);
            return Task.FromResult(request.RequestUri!.AbsolutePath == "/files/a.zip"
                ? Redirect("https://registry.example.com/cdn/a.zip")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        });
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);

        await using var download = (await Client(handler).DownloadAsync(release, CancellationToken.None, "key-123")).Value!;

        keys.Should().Equal("key-123", "key-123");
    }

    /// <summary>A 401 on a release lookup or a download is "licence required", a 403 is "licence invalid", and no text of the reply is kept.</summary>
    [Fact]
    public async Task LicenceStatuses_BecomeDistinctCodesAsync()
    {
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);
        var unauthorized = () => ScriptedHttpMessageHandler.Json("secret upstream text", HttpStatusCode.Unauthorized);
        var forbidden = () => ScriptedHttpMessageHandler.Json("secret upstream text", HttpStatusCode.Forbidden);

        var released = new[]
        {
            await Client(unauthorized()).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None),
            await Client(forbidden()).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None, "wrong"),
        };
        var downloaded = new[]
        {
            await Client(unauthorized()).DownloadAsync(release, CancellationToken.None),
            await Client(forbidden()).DownloadAsync(release, CancellationToken.None, "wrong"),
        };

        released.Select(r => r.ErrorCode).Should().Equal(
            HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_REQUIRED, HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
        downloaded.Select(r => r.ErrorCode).Should().Equal(
            HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_REQUIRED, HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
        released.Should().OnlyContain(r => r.Error == null && r.Errors == null);
        downloaded.Should().OnlyContain(r => r.Error == null && r.Errors == null);
        StagedFiles().Should().BeEmpty();
    }

    /// <summary>A 401 or 403 on the anonymous calls (listing, newest versions) is not a licence matter: the registry is simply unavailable.</summary>
    [Fact]
    public async Task AnonymousCalls_DoNotMapLicenceStatusesAsync()
    {
        var search = new RegistryPluginSearch(null, null, 1, 20);

        (await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized)).SearchAsync(search, CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        (await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.Forbidden)).GetLatestAsync(["acme-pay"], CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
    }

    /// <summary>A key that cannot be a header value is refused as invalid before anything is sent, and the key is not in the result.</summary>
    /// <param name="key">The key.</param>
    [Theory]
    [InlineData("has space")]
    [InlineData("line\nbreak")]
    [InlineData("café")]
    public async Task AMalformedLicenceKey_IsRefusedWithoutARequestAsync(string key)
    {
        var handler = ScriptedHttpMessageHandler.Json("{}");

        var result = await Client(handler).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None, key);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
        result.Error.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A plugin page is read: the summary, the external links, the licence and the latest version with its images.</summary>
    [Fact]
    public async Task GetPlugin_ReadsThePageAsync()
    {
        var keys = new List<string?>();
        var handler = KeyRecorder(keys, HttpStatusCode.OK, $$"""
            {"summary":{{EntryJson("acme-pay")}},"licence":"MIT","repository":"https://example.org/acme/pay","buyUrl":"javascript:alert(1)",
             "contactUrl":"mailto:a@example.org","documentationUrl":"https://docs.example.org/","supportUrl":"/relative",
             "latest":{{VersionJson("1.2.3", ",\"icon\":{\"url\":\"/media/icon.png\",\"contentType\":\"image/png\",\"sizeBytes\":10,\"width\":64,\"height\":64}")}}
            }
            """);

        var result = await Client(handler).GetPluginAsync("acme-pay", "key-123", CancellationToken.None);

        var details = result.Value!;
        details.Summary.Id.Should().Be("acme-pay");
        details.Licence.Should().Be("MIT");
        details.Repository.Should().Be("https://example.org/acme/pay");
        details.DocumentationUrl.Should().Be("https://docs.example.org/");
        details.BuyUrl.Should().BeNull("only http and https links are kept");
        details.ContactUrl.Should().BeNull();
        details.SupportUrl.Should().BeNull("a link must be absolute");
        details.Latest!.Version.Should().Be("1.2.3");
        details.Latest.Icon.Should().Be(new RegistryPluginImage("https://registry.example.com/media/icon.png", "image/png", 64, 64));
        handler.Requests.Single().AbsoluteUri.Should().Be("https://registry.example.com/api/plugins/acme-pay");
        keys.Should().Equal("key-123");
    }

    /// <summary>A plugin page with nothing published keeps its summary with an empty latest version.</summary>
    [Fact]
    public async Task GetPlugin_WithoutALatestVersion_KeepsTheSummaryAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("""
            {"summary":{"pluginId":"acme-pay","name":"Name","latestVersion":null,"pricing":"Free"},"latest":null}
            """);

        var details = (await Client(handler).GetPluginAsync("acme-pay", null, CancellationToken.None)).Value!;

        details.Summary.LatestVersion.Should().BeEmpty();
        details.Latest.Should().BeNull();
    }

    /// <summary>A 404 for a plugin page is a successful answer holding nothing; a 401 and a 403 are the licence codes; a bad summary is invalid.</summary>
    [Fact]
    public async Task GetPlugin_HandlesNotFoundLicenceAndBadSummaryAsync()
    {
        var missing = await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.NotFound)).GetPluginAsync("acme-pay", null, CancellationToken.None);
        var required = await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized)).GetPluginAsync("acme-pay", null, CancellationToken.None);
        var invalidKey = await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.Forbidden)).GetPluginAsync("acme-pay", "wrong", CancellationToken.None);
        var bad = await Client(ScriptedHttpMessageHandler.Json("""{"summary":{"pluginId":"Bad Id","name":"n","pricing":"Free"}}""")).GetPluginAsync("acme-pay", null, CancellationToken.None);

        missing.IsSuccess.Should().BeTrue();
        missing.Value.Should().BeNull();
        required.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_REQUIRED);
        invalidKey.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
        bad.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
    }

    /// <summary>A version list is read; relative image addresses are resolved on the registry, foreign ones dropped, bad rows left out.</summary>
    [Fact]
    public async Task ListVersions_ReadsTheVersionsAsync()
    {
        var keys = new List<string?>();
        var shots = """
            ,"permissions":["Process","FileSystem","Unknown","process"],"changelog":"Line one\r\nLine two\u0007",
            "minHostVersion":"1.0.0","maxHostVersion":null,
            "screenshots":[{"url":"/media/s1.png","contentType":"image/png","width":800,"height":600},{"url":"https://evil.example.net/s2.png","contentType":"image/png"}]
            """;
        var handler = KeyRecorder(keys, HttpStatusCode.OK, $$"""
            [{{VersionJson("2.0.0", shots)}},
             {{VersionJson("1.0.0").Replace("Published", "Yanked").Replace("\"statusReason\":null", "\"statusReason\":\"Broken\"").Replace("\"replacementVersion\":null", "\"replacementVersion\":\"2.0.0\"")}},
             {{VersionJson("0.5.0").Replace("Published", "Mystery")}},
             {{VersionJson("not-a-version")}}]
            """);

        var result = await Client(handler).ListVersionsAsync("acme-pay", "key-123", CancellationToken.None);

        var versions = result.Value!;
        versions.Select(v => (v.Version, v.Status)).Should().Equal(
            ("2.0.0", PluginRegistryVersionStatus.Published), ("1.0.0", PluginRegistryVersionStatus.Yanked));
        var first = versions[0];
        first.Permissions.Should().Equal(PluginRegistryCapability.Process, PluginRegistryCapability.FileSystem);
        first.Changelog.Should().Be("Line one\nLine two");
        first.MinHostVersion.Should().Be("1.0.0");
        first.MaxHostVersion.Should().BeNull();
        first.SizeBytes.Should().Be(2048);
        first.PublishedAt.Should().Be(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        first.Icon.Should().BeNull();
        first.Screenshots.Should().Equal(new RegistryPluginImage("https://registry.example.com/media/s1.png", "image/png", 800, 600));
        versions[1].StatusReason.Should().Be("Broken");
        versions[1].ReplacementVersion.Should().Be("2.0.0");
        handler.Requests.Single().AbsoluteUri.Should().Be("https://registry.example.com/api/plugins/acme-pay/versions");
        keys.Should().Equal("key-123");
    }

    /// <summary>A version list answers not found for a 404 and the licence codes for a 401 and a 403.</summary>
    [Fact]
    public async Task ListVersions_MapsFailureStatusesAsync()
    {
        (await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.NotFound)).ListVersionsAsync("acme-pay", null, CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
        (await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized)).ListVersionsAsync("acme-pay", null, CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_REQUIRED);
        (await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.Forbidden)).ListVersionsAsync("acme-pay", "wrong", CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
        (await Client(ScriptedHttpMessageHandler.Json("""{"not":"an array"}""")).ListVersionsAsync("acme-pay", null, CancellationToken.None))
            .ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
    }

    /// <summary>The new reads are refused with the disabled code when the registry is off.</summary>
    [Fact]
    public async Task ThePluginReads_AreDisabledWithTheRegistryAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("{}");
        var client = Client(handler, enabled: false);

        (await client.GetPluginAsync("acme-pay", null, CancellationToken.None)).ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        (await client.ListVersionsAsync("acme-pay", null, CancellationToken.None)).ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A redirect to another origin is refused while the licence key is set, and the other host receives no request.</summary>
    [Fact]
    public async Task ACrossOriginRedirect_WithAKey_IsRefusedBeforeReachingTheOtherHostAsync()
    {
        var hosts = new List<string>();
        var handler = new ScriptedHttpMessageHandler((request, _) =>
        {
            hosts.Add(request.RequestUri!.Host);
            return Task.FromResult(request.RequestUri.Host == "registry.example.com"
                ? Redirect("https://evil.example.net/a.zip")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });
        });
        var release = new RegistryPluginRelease("acme-pay", "1.2.3", new Uri("https://registry.example.com/files/a.zip"), Sha, true);

        var result = await Client(handler).DownloadAsync(release, CancellationToken.None, "key-123");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        hosts.Should().Equal("registry.example.com");
    }

    /// <summary>A null <c>verified</c> or <c>requiresLicence</c> in the newest-version reply and in a release reads as false.</summary>
    [Fact]
    public async Task NullFlags_ReadAsFalseAsync()
    {
        var latest = ScriptedHttpMessageHandler.Json("""
            [{"pluginId":"acme-pay","version":"1.2.3","downloadUrl":"/x.zip","sha256":"abc","verified":null,"requiresLicence":null}]
            """);
        var release = ScriptedHttpMessageHandler.Json($$"""{"downloadUrl":"/a.zip","sha256":"{{Sha}}","verified":null}""");

        var plugins = await Client(latest).GetLatestAsync(["acme-pay"], CancellationToken.None);
        var found = await Client(release).GetReleaseAsync(new RegistryPluginVersionRef("acme-pay", "1.2.3"), CancellationToken.None);

        plugins.Value!.Single().Verified.Should().BeFalse();
        plugins.Value!.Single().Pricing.Should().Be(PluginRegistryPricing.Free);
        found.Value!.Verified.Should().BeFalse();
    }

    /// <summary>A version listed twice with different statuses keeps one row with the strictest status.</summary>
    [Fact]
    public async Task ListVersions_ADuplicateVersion_KeepsTheStrictestStatusAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""
            [{{VersionJson("1.0.0")}},
             {{VersionJson("1.0.0").Replace("Published", "Yanked")}},
             {{VersionJson("1.0.0").Replace("Published", "Deprecated")}}]
            """);

        var result = await Client(handler).ListVersionsAsync("acme-pay", null, CancellationToken.None);

        result.Value!.Select(v => (v.Version, v.Status)).Should().Equal(("1.0.0", PluginRegistryVersionStatus.Yanked));
    }

    /// <summary>A key with a space, a non-ASCII character or over 256 characters is refused without a request; 256 is sent.</summary>
    [Fact]
    public async Task AKeyBreakingTheRule_IsRefusedWithoutARequestAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json("{}");
        var release = new RegistryPluginVersionRef("acme-pay", "1.2.3");

        foreach (var key in new[] { "has space", "kéy", new string('k', 257) })
        {
            (await Client(handler).GetReleaseAsync(release, CancellationToken.None, key)).ErrorCode
                .Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
        }

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>Builds a redirect response.</summary>
    /// <param name="location">Where it points.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location);
        return response;
    }
}
