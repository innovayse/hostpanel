using System.Net;
using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins.Registry;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistryClient"/> against a scripted HTTP handler: the wire shapes it reads, that every request and
/// redirect stays on the registry's host, the size and time limits, and that a failure becomes a
/// <c>PLUGIN_REGISTRY_*</c> code with nothing of the upstream reply in it.
/// </summary>
public sealed class PluginRegistryClientTests : IDisposable
{
    /// <summary>An upload folder of this test's own, so the staging folder the downloads use is isolated and checked.</summary>
    private readonly string _installPath = Path.Combine(Path.GetTempPath(), $"registry-client-{Guid.NewGuid():N}");

    /// <summary>Deletes the test's upload folder.</summary>
    public void Dispose()
    {
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
        return new PluginRegistryClient(
            new HttpClient(handler), options, Options.Create(new PluginOptions { InstallPath = _installPath }), NullLogger<PluginRegistryClient>.Instance);
    }

    /// <summary>One valid plugin as the registry sends it.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="extra">Further members, starting with a comma; empty for none.</param>
    /// <returns>The JSON object text.</returns>
    private static string EntryJson(string id, string extra = "")
    {
        return $$"""{"id":"{{id}}","name":"Name","description":"d","author":"a","category":"Payment","latestVersion":"1.2.3","sdkVersion":"1.0","verified":true,"pricing":"Paid"{{extra}}}""";
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
        entry.Should().Be(new RegistryPluginDto("acme-pay", "Name", "a", "d", null, PluginRegistryCategory.Payment, "1.2.3", "1.0", true, PluginRegistryPricing.Paid));
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

    /// <summary>An entry that is not a plugin (bad id, bad version, no pricing) fails the whole reply as invalid.</summary>
    /// <param name="entry">The entry JSON.</param>
    [Theory]
    [InlineData("""{"id":"Bad Id","name":"n","latestVersion":"1.0.0","pricing":"Free"}""")]
    [InlineData("""{"id":"ok-id","name":"n","latestVersion":"latest","pricing":"Free"}""")]
    [InlineData("""{"id":"ok-id","name":"n","latestVersion":"1.0.0","pricing":"Gold"}""")]
    [InlineData("""{"id":"ok-id","name":"","latestVersion":"1.0.0","pricing":"Free"}""")]
    public async Task Search_AnInvalidEntry_FailsTheReplyAsync(string entry)
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"items":[{{entry}}],"total":1}""");

        var result = await Client(handler).SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
    }

    /// <summary>The newest-version lookup posts the ids as JSON.</summary>
    [Fact]
    public async Task GetLatest_PostsTheIdsAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"items":[{{EntryJson("acme-pay")}}],"total":1}""");

        var result = await Client(handler).GetLatestAsync(["acme-pay", "other"], CancellationToken.None);

        result.Value!.Single().Id.Should().Be("acme-pay");
        handler.Requests.Single().AbsoluteUri.Should().Be("https://registry.example.com/api/plugins/latest");
        handler.Bodies.Single().Should().Be("""{"ids":["acme-pay","other"]}""");
    }

    /// <summary>A release is resolved, a relative package address is made absolute on the registry's host, the checksum is lower-cased.</summary>
    [Fact]
    public async Task GetRelease_ResolvesARelativePackageAddressAsync()
    {
        var handler = ScriptedHttpMessageHandler.Json($$"""{"pluginId":"acme-pay","version":"1.2.3","downloadUrl":"/files/acme.zip","sha256":"{{Sha.ToUpperInvariant()}}","verified":true}""");

        var result = await Client(handler).GetReleaseAsync("acme-pay", "1.2.3", CancellationToken.None);

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

        var result = await Client(handler).GetReleaseAsync("acme-pay", "1.2.3", CancellationToken.None);

        result.ErrorCode.Should().Be(code);
    }

    /// <summary>A 404 for a version is <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task GetRelease_A404_IsNotFoundAsync()
    {
        var result = await Client(ScriptedHttpMessageHandler.Json("{}", HttpStatusCode.NotFound)).GetReleaseAsync("acme-pay", "9.9.9", CancellationToken.None);

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
