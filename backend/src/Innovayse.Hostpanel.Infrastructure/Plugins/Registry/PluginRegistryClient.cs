using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// The plugin registry over HTTPS. It talks to the configured address only: every request, every redirect hop and every
/// package address must have the registry's scheme, host and port, a redirect is followed by hand and only inside that
/// origin, and every body is read with a size limit. A package is streamed to a temporary file in the plugin staging
/// folder, never held in memory. A failure becomes a <c>PLUGIN_REGISTRY_*</c> result; what the registry or the network
/// said is never put in it, only the exception type reaches the log.
/// </summary>
/// <remarks>
/// A plugin the registry distributes to installations only (<see cref="PluginRegistryDistribution.HostOnly"/>) is asked for
/// differently: the version list, the version details, the download ticket and the final package fetch each carry the
/// signature headers of this installation's registry identity (a fresh one on every hop, so a redirect inside the origin is
/// signed for its own address) and never a licence key. The registry's refusal of a signed call is read only for its error
/// code and becomes a <c>PLUGIN_REGISTRY_INSTANCE_*</c> code with a sentence for the admin.
/// </remarks>
public sealed partial class PluginRegistryClient : IPluginRegistryClient
{
    /// <summary>The most redirect hops one request follows.</summary>
    private const int MaxRedirects = 3;

    /// <summary>The most bytes a JSON reply may hold (1 MiB).</summary>
    private const long MaxJsonBytes = 1_048_576;

    /// <summary>The longest display name or author kept from the registry.</summary>
    private const int MaxNameLength = 200;

    /// <summary>The longest description kept from the registry.</summary>
    private const int MaxDescriptionLength = 2000;

    /// <summary>The longest SDK version text kept from the registry.</summary>
    private const int MaxSdkVersionLength = 32;

    /// <summary>The longest external link (repository, buy, contact, documentation, support) kept from the registry.</summary>
    private const int MaxLinkLength = 500;

    /// <summary>The longest licence name, status reason or image media type kept from the registry.</summary>
    private const int MaxShortTextLength = 200;

    /// <summary>The longest changelog kept from the registry.</summary>
    private const int MaxChangelogLength = 20000;

    /// <summary>The most tags kept for one plugin.</summary>
    private const int MaxTags = 20;

    /// <summary>The longest tag kept from the registry.</summary>
    private const int MaxTagLength = 50;

    /// <summary>The most screenshots kept for one version.</summary>
    private const int MaxScreenshots = 20;

    /// <summary>The header that carries the licence key of a paid or private plugin.</summary>
    private const string LicenceHeader = "X-Licence-Key";

    /// <summary>The most bytes of a refusal's body read to find its error code (64 KiB).</summary>
    private const int MaxRefusalBytes = 65_536;

    /// <summary>The <c>Accept</c> value of a JSON call.</summary>
    private const string AcceptJson = "application/json";

    /// <summary>The <c>Accept</c> value of a package download.</summary>
    private const string AcceptPackage = "application/zip, application/octet-stream";

    /// <summary>
    /// The staging folder's name inside the upload folder, the same one the package store works in: it fails the plugin
    /// id rule so it is never loaded, and the host sweeps it at startup, which removes a file a crash left behind.
    /// </summary>
    private const string StagingFolderName = ".staging";

    /// <summary>The wire's JSON conventions: camelCase names, case-insensitive reads.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The HTTP client. It must not follow redirects on its own; this class does it, inside the registry's origin.</summary>
    private readonly HttpClient _http;

    /// <summary>The registry configuration.</summary>
    private readonly PluginRegistryOptions _options;

    /// <summary>The plugin folders, for the staging folder.</summary>
    private readonly PluginOptions _pluginOptions;

    /// <summary>Signs the calls for a restricted plugin with this installation's registry identity.</summary>
    private readonly IRegistryRequestSigner _signer;

    /// <summary>Logs a failed call by exception type only.</summary>
    private readonly ILogger<PluginRegistryClient> _logger;

    /// <summary>Initializes the client.</summary>
    /// <param name="http">The HTTP client, built with automatic redirects off.</param>
    /// <param name="options">The registry configuration.</param>
    /// <param name="pluginOptions">The plugin folders, for the staging folder.</param>
    /// <param name="signer">Signs the calls for a restricted plugin with this installation's registry identity.</param>
    /// <param name="logger">Logs a failed call by exception type only.</param>
    public PluginRegistryClient(
        HttpClient http,
        IOptions<PluginRegistryOptions> options,
        IOptions<PluginOptions> pluginOptions,
        IRegistryRequestSigner signer,
        ILogger<PluginRegistryClient> logger)
    {
        _http = http;
        _options = options.Value;
        _pluginOptions = pluginOptions.Value;
        _signer = signer;
        _logger = logger;
    }

    /// <summary>A SHA-256 in hex.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    /// <inheritdoc/>
    public Task<Result<RegistryPluginPageDto>> SearchAsync(RegistryPluginSearch search, CancellationToken ct)
    {
        return RunAsync(async (baseUri, token) =>
        {
            var query = new List<string>();
            if (search.Search is not null)
            {
                query.Add($"q={Uri.EscapeDataString(search.Search)}");
            }

            if (search.Category is { } category)
            {
                query.Add($"category={category}");
            }

            query.Add($"page={search.Page}");
            query.Add($"pageSize={search.PageSize}");

            var wire = await GetJsonAsync<PluginRegistryPageWire>(
                new Uri(baseUri, "plugins?" + string.Join("&", query)), baseUri, HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE, token);
            return new RegistryPluginPageDto(ToPlugins(wire, baseUri), Math.Max(0, wire.Total), search.Page, search.PageSize);
        }, ct);
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<RegistryPluginDto>>> GetLatestAsync(IReadOnlyCollection<string> pluginIds, CancellationToken ct)
    {
        return RunAsync<IReadOnlyList<RegistryPluginDto>>(async (baseUri, token) =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "plugins/latest"))
            {
                Content = JsonContent.Create(new PluginRegistryLatestWire(pluginIds), options: Json),
            };
            using var response = await SendAsync(request, baseUri, AcceptJson, HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE, token);
            var wire = await ReadJsonAsync<List<PluginRegistryReleaseWire?>>(response, token);
            return ToLatest(wire);
        }, ct);
    }

    /// <inheritdoc/>
    public Task<Result<RegistryPluginRelease>> GetReleaseAsync(RegistryPluginVersionRef target, CancellationToken ct, string? licenceKey = null)
    {
        var (pluginId, version) = (target.PluginId, target.Version);
        var restricted = target.Distribution == PluginRegistryDistribution.HostOnly;
        return RunAsync(async (baseUri, token) =>
        {
            // A restricted plugin is asked for with the identity's signature and never with a licence key.
            var key = restricted ? null : NormaliseLicenceKey(licenceKey);
            // The id and version have the validated shape, and are escaped all the same: they become path segments.
            // A 404 here, and only here, means the registry does not list that version.
            var url = new Uri(baseUri, $"plugins/{Uri.EscapeDataString(pluginId)}/versions/{Uri.EscapeDataString(version)}");
            var wire = await GetJsonAsync<PluginRegistryReleaseWire>(
                url, baseUri, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND, token, key, licenceGated: !restricted, signed: restricted);

            if (wire.Sha256 is null || !Sha256Pattern().IsMatch(wire.Sha256) || string.IsNullOrWhiteSpace(wire.DownloadUrl))
            {
                throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
            }

            // The package may live only on the registry's own host: an address elsewhere is refused before anything is fetched.
            if (!Uri.TryCreate(baseUri, wire.DownloadUrl, out var download) || !SameOrigin(download, baseUri))
            {
                throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
            }

            return new RegistryPluginRelease(
                wire.PluginId ?? pluginId, wire.Version ?? version, download, wire.Sha256.ToLowerInvariant(), wire.Verified ?? false, target.Distribution);
        }, ct);
    }

    /// <inheritdoc/>
    public Task<Result<Stream>> DownloadAsync(RegistryPluginRelease release, CancellationToken ct, string? licenceKey = null)
    {
        var restricted = release.Distribution == PluginRegistryDistribution.HostOnly;
        return RunAsync<Stream>(async (baseUri, token) =>
        {
            var key = restricted ? null : NormaliseLicenceKey(licenceKey);
            if (!SameOrigin(release.DownloadUrl, baseUri))
            {
                throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
            }

            // A restricted package is fetched only against a one time ticket, issued to this installation by a signed call; the
            // address the ticket names replaces the one the version details named.
            var source = restricted ? await IssueTicketAsync(release, baseUri, token) : release.DownloadUrl;
            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            using var response = await SendAsync(
                request, baseUri, AcceptPackage, HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE, token, key, licenceGated: !restricted, signed: restricted);

            // The file is deleted when the stream is disposed, and here at once when anything below fails.
            var staging = Path.Combine(_pluginOptions.InstallRoot, StagingFolderName);
            Directory.CreateDirectory(staging);
            var file = new FileStream(
                Path.Combine(staging, $"registry-{Guid.NewGuid():N}.zip"),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            try
            {
                await CopyLimitedAsync(response.Content, file, _options.MaxDownloadBytes, HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED, token);
                file.Position = 0;
                return file;
            }
            catch
            {
                await file.DisposeAsync();
                throw;
            }
        }, ct);
    }

    /// <inheritdoc/>
    public Task<Result<RegistryPluginDetails?>> GetPluginAsync(string pluginId, string? licenceKey, CancellationToken ct)
    {
        return RunAsync<RegistryPluginDetails?>(async (baseUri, token) =>
        {
            var key = NormaliseLicenceKey(licenceKey);
            var url = new Uri(baseUri, $"plugins/{Uri.EscapeDataString(pluginId)}");
            PluginRegistryDetailWire wire;
            try
            {
                wire = await GetJsonAsync<PluginRegistryDetailWire>(url, baseUri, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND, token, key, licenceGated: true);
            }
            catch (PluginRegistryException ex) when (ex.Code == HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND)
            {
                // A plugin the registry does not list is an answer, not a failure: the page shows "not found".
                return null;
            }

            var summary = ToPlugin(wire.Summary, baseUri);
            var latest = wire.Latest is null ? null : ToVersion(wire.Latest, baseUri);
            return new RegistryPluginDetails(
                summary.LatestVersion.Length == 0 && latest is not null ? summary with { LatestVersion = latest.Version } : summary,
                CleanOrNull(wire.Licence, MaxShortTextLength),
                ToWebLink(wire.Repository),
                ToWebLink(wire.BuyUrl),
                ToWebLink(wire.ContactUrl),
                ToWebLink(wire.DocumentationUrl),
                ToWebLink(wire.SupportUrl),
                latest);
        }, ct);
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<RegistryPluginVersion>>> ListVersionsAsync(
        string pluginId, string? licenceKey, CancellationToken ct, PluginRegistryDistribution distribution = PluginRegistryDistribution.Download)
    {
        var restricted = distribution == PluginRegistryDistribution.HostOnly;
        return RunAsync<IReadOnlyList<RegistryPluginVersion>>(async (baseUri, token) =>
        {
            var key = restricted ? null : NormaliseLicenceKey(licenceKey);
            var url = new Uri(baseUri, $"plugins/{Uri.EscapeDataString(pluginId)}/versions");
            var wire = await GetJsonAsync<List<PluginRegistryVersionWire?>>(
                url, baseUri, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND, token, key, licenceGated: !restricted, signed: restricted);
            var versions = new List<RegistryPluginVersion>();
            foreach (var item in wire)
            {
                var version = item is null ? null : ToVersion(item, baseUri);
                if (version is null)
                {
                    continue;
                }

                // A version listed twice keeps its first place and the strictest status, so a yanked row cannot be
                // hidden behind a published one.
                var at = versions.FindIndex(v => string.Equals(v.Version, version.Version, StringComparison.Ordinal));
                if (at < 0)
                {
                    versions.Add(version);
                }
                else if (version.Status.Strictness() > versions[at].Status.Strictness())
                {
                    versions[at] = version;
                }
            }

            return versions;
        }, ct);
    }

    /// <summary>
    /// Runs one registry operation under the time limit and turns every failure into a result. A cancellation the caller
    /// did not ask for is the time limit.
    /// </summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="operation">The operation, given the registry address and the limited token.</param>
    /// <param name="ct">The caller's token.</param>
    /// <returns>The value, or a <c>PLUGIN_REGISTRY_*</c> failure.</returns>
    private async Task<Result<T>> RunAsync<T>(Func<Uri, CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return Result<T>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        }

        using var limited = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limited.CancelAfter(_options.Timeout);
        try
        {
            var baseUri = new Uri(_options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/");
            return Result<T>.Success(await operation(baseUri, limited.Token));
        }
        catch (PluginRegistryException ex)
        {
            return Result<T>.FailureCode(ex.Code);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<T>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_TIMED_OUT);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or InvalidOperationException
            or IOException or UnauthorizedAccessException or UriFormatException)
        {
            // Only the type is logged: the message may carry the address or part of the registry's reply.
            LogCallFailed(ex.GetType().Name);
            return Result<T>.FailureCode(ex is JsonException or NotSupportedException or UriFormatException
                ? HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID
                : HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        }
    }

    /// <summary>Logs that a registry call failed.</summary>
    /// <param name="exceptionType">The name of the exception type.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A plugin registry call failed: {ExceptionType}.")]
    private partial void LogCallFailed(string exceptionType);

    /// <summary>Sends a GET and reads a JSON reply.</summary>
    /// <typeparam name="T">The wire type.</typeparam>
    /// <param name="url">The address, inside the registry's origin.</param>
    /// <param name="baseUri">The registry address.</param>
    /// <param name="notFoundCode">The error code a 404 answers with.</param>
    /// <param name="ct">The limited token.</param>
    /// <param name="licenceKey">The validated licence key to send, or <see langword="null"/>.</param>
    /// <param name="licenceGated">Whether a 401 or 403 means a licence key is required or invalid.</param>
    /// <param name="signed">Whether the call carries this installation's signature headers; no licence key is sent with them.</param>
    /// <returns>The parsed reply.</returns>
    private async Task<T> GetJsonAsync<T>(
        Uri url, Uri baseUri, string notFoundCode, CancellationToken ct, string? licenceKey = null, bool licenceGated = false, bool signed = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, baseUri, AcceptJson, notFoundCode, ct, licenceKey, licenceGated, signed);
        return await ReadJsonAsync<T>(response, ct);
    }

    /// <summary>
    /// Asks for the one time ticket of a restricted package with a signed call and checks the address it names: it must be on
    /// the registry's origin and carry the ticket, so a registry cannot point the signed package fetch elsewhere.
    /// </summary>
    /// <param name="release">The release to fetch.</param>
    /// <param name="baseUri">The registry address.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>The address the package is fetched from.</returns>
    /// <exception cref="PluginRegistryException">The registry refused, or named an address that is not usable.</exception>
    private async Task<Uri> IssueTicketAsync(RegistryPluginRelease release, Uri baseUri, CancellationToken ct)
    {
        var url = new Uri(
            baseUri,
            $"plugins/{Uri.EscapeDataString(release.PluginId)}/versions/{Uri.EscapeDataString(release.Version)}/download-tickets");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        using var response = await SendAsync(
            request, baseUri, AcceptJson, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND, ct, licenceKey: null, licenceGated: false, signed: true);
        var wire = await ReadJsonAsync<PluginRegistryTicketWire>(response, ct);

        if (string.IsNullOrWhiteSpace(wire.Ticket)
            || string.IsNullOrWhiteSpace(wire.DownloadUrl)
            || !Uri.TryCreate(baseUri, wire.DownloadUrl, out var address)
            || !SameOrigin(address, baseUri)
            || !address.Query.Contains(wire.Ticket, StringComparison.Ordinal))
        {
            throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        }

        return address;
    }

    /// <summary>Reads a JSON reply under the size limit.</summary>
    /// <typeparam name="T">The wire type.</typeparam>
    /// <param name="response">The successful response.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>The parsed reply; a reply of <c>null</c> is not a reply.</returns>
    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await using var body = new MemoryStream();
        await CopyLimitedAsync(response.Content, body, MaxJsonBytes, HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID, ct);
        body.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(body, Json, ct)
            ?? throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
    }

    /// <summary>
    /// Sends a request and follows redirects by hand, only for GET and only inside the registry's origin. The
    /// <c>Accept</c> header is set on every request, redirect hops included.
    /// </summary>
    /// <param name="request">The first request; its address must be inside the registry's origin.</param>
    /// <param name="baseUri">The registry address.</param>
    /// <param name="accept">The media types the caller accepts.</param>
    /// <param name="notFoundCode">The error code a 404 answers with: only a release lookup treats it as "not listed".</param>
    /// <param name="ct">The limited token.</param>
    /// <param name="licenceKey">
    /// The validated licence key, sent in <c>X-Licence-Key</c> on every hop (all hops are inside the registry's origin), or
    /// <see langword="null"/> to send none. It is never logged and never part of an error.
    /// </param>
    /// <param name="licenceGated">Whether a 401 means a key is required and a 403 that the key is invalid.</param>
    /// <param name="signed">
    /// Whether every hop carries the signature headers of this installation's registry identity (and then never a licence key).
    /// A 401, 403 or 429 is then read for its registry error code and becomes a <c>PLUGIN_REGISTRY_INSTANCE_*</c> code.
    /// </param>
    /// <returns>A successful response; the caller disposes it.</returns>
    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, Uri baseUri, string accept, string notFoundCode, CancellationToken ct, string? licenceKey = null, bool licenceGated = false, bool signed = false)
    {
        var current = request;
        var created = new List<HttpRequestMessage>();
        try
        {
            for (var hop = 0; ; hop++)
            {
                if (!SameOrigin(current.RequestUri!, baseUri))
                {
                    throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
                }

                current.Headers.Accept.ParseAdd(accept);
                if (signed)
                {
                    await SignAsync(current, ct);
                }
                else if (licenceKey is not null)
                {
                    current.Headers.TryAddWithoutValidation(LicenceHeader, licenceKey);
                }

                var response = await _http.SendAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var status = response.StatusCode;
                var location = response.Headers.Location;
                var refusal = signed && status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
                    ? await RefusalCodeAsync(response, status, ct)
                    : null;
                response.Dispose();

                if (refusal is not null)
                {
                    // Told to the identity too, so the admin page shows a revocation as it is.
                    await _signer.ReportRefusalAsync(refusal, ct);
                    throw new PluginRegistryException(refusal);
                }

                if (status == HttpStatusCode.NotFound)
                {
                    throw new PluginRegistryException(notFoundCode);
                }

                if (licenceGated && status == HttpStatusCode.Unauthorized)
                {
                    throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_REQUIRED);
                }

                if (licenceGated && status == HttpStatusCode.Forbidden)
                {
                    throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
                }

                var redirect = (int)status is 301 or 302 or 303 or 307 or 308;
                if (!redirect || location is null || current.Method != HttpMethod.Get || hop >= MaxRedirects)
                {
                    throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
                }

                // A hop to another host (or scheme, or port) is refused, never followed.
                var next = new Uri(current.RequestUri!, location);
                if (!SameOrigin(next, baseUri))
                {
                    throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
                }

                current = new HttpRequestMessage(HttpMethod.Get, next);
                created.Add(current);
            }
        }
        finally
        {
            foreach (var message in created)
            {
                message.Dispose();
            }
        }
    }

    /// <summary>Adds the identity's signature headers to a request, for the exact target it is about to send.</summary>
    /// <param name="request">The request; it has a method and an absolute address, and no body.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>A task that completes once the headers are set.</returns>
    /// <exception cref="PluginRegistryException">The identity cannot sign: it is not verified, or it is revoked.</exception>
    private async Task SignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // The target is what the request line will say: the escaped path and the query, ticket included. The registry rebuilds
        // the signed text from the line it receives, so both sides must read the same bytes.
        var signature = await _signer.SignAsync(request.Method.Method, request.RequestUri!.PathAndQuery, ct);
        if (signature.IsFailure)
        {
            throw new PluginRegistryException(signature.ErrorCode!);
        }

        var headers = signature.Value!;
        request.Headers.TryAddWithoutValidation(RegistryCallSigning.InstanceHeader, headers.InstanceId.ToString("D"));
        request.Headers.TryAddWithoutValidation(RegistryCallSigning.TimestampHeader, headers.Timestamp);
        request.Headers.TryAddWithoutValidation(RegistryCallSigning.NonceHeader, headers.Nonce);
        request.Headers.TryAddWithoutValidation(RegistryCallSigning.SignatureHeader, headers.Signature);
    }

    /// <summary>
    /// Turns the registry's refusal of a signed call into a host code. Only the <c>type</c> of the error body is read, from a
    /// bounded read; a body that is missing, too long or not JSON reads as an unknown code, and the status decides.
    /// </summary>
    /// <param name="response">The refusing response.</param>
    /// <param name="status">Its status: 401, 403 or 429.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>The <c>PLUGIN_REGISTRY_*</c> code the refusal answers with.</returns>
    private static async Task<string> RefusalCodeAsync(HttpResponseMessage response, HttpStatusCode status, CancellationToken ct)
    {
        string? registryCode = null;
        try
        {
            using var body = new MemoryStream();
            await CopyLimitedAsync(response.Content, body, MaxRefusalBytes, HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID, ct);
            body.Position = 0;
            registryCode = (await JsonSerializer.DeserializeAsync<PluginRegistryErrorWire>(body, Json, ct))?.Type;
        }
        catch (Exception ex) when (ex is JsonException or PluginRegistryException or NotSupportedException or IOException or InvalidOperationException)
        {
            // An unreadable body is an unknown code: the status alone says what kind of refusal it was.
        }

        return (status, registryCode) switch
        {
            (HttpStatusCode.TooManyRequests, _) => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED,
            (HttpStatusCode.Unauthorized, "INSTANCE_TIMESTAMP_UNAUTHORIZED") => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_CLOCK_SKEW,
            (HttpStatusCode.Unauthorized, "INSTANCE_REPLAY_UNAUTHORIZED") => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REPLAY_REJECTED,
            (HttpStatusCode.Unauthorized, _) => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_SIGNATURE_REJECTED,
            (_, "INSTANCE_REVOKED_FORBIDDEN") => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED,
            (_, "INSTANCE_NOT_VERIFIED_FORBIDDEN") => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED,
            (_, "DOWNLOAD_TICKET_INVALID_FORBIDDEN") => HostpanelErrorCodes.PLUGIN_REGISTRY_DOWNLOAD_TICKET_REFUSED,
            (_, "VERSION_COMPROMISED_FORBIDDEN") => HostpanelErrorCodes.PLUGIN_REGISTRY_VERSION_COMPROMISED,
            _ => HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE,
        };
    }

    /// <summary>Copies a body into a stream, refusing it as soon as it is over the limit.</summary>
    /// <param name="content">The response content.</param>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="maxBytes">The most bytes accepted.</param>
    /// <param name="overLimitCode">The error code for a body over the limit.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>A task that completes once the body is copied.</returns>
    private static async Task CopyLimitedAsync(HttpContent content, Stream destination, long maxBytes, string overLimitCode, CancellationToken ct)
    {
        // The declared length refuses an oversized body before reading a byte; the counted write below is the real limit.
        if (content.Headers.ContentLength is { } declared && declared > maxBytes)
        {
            throw new PluginRegistryException(overLimitCode);
        }

        await using var source = await content.ReadAsStreamAsync(ct);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new PluginRegistryException(overLimitCode);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    /// <summary>Whether an address has the registry's scheme, host and port.</summary>
    /// <param name="candidate">The address to test.</param>
    /// <param name="baseUri">The registry address.</param>
    /// <returns><see langword="true"/> for the same origin and no user info.</returns>
    private static bool SameOrigin(Uri candidate, Uri baseUri)
    {
        return candidate.IsAbsoluteUri
            && string.IsNullOrEmpty(candidate.UserInfo)
            && string.Equals(candidate.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            && candidate.Port == baseUri.Port;
    }

    /// <summary>
    /// Checks every listed plugin and turns it into the application type; an entry that is not a plugin fails the whole
    /// reply, but an entry with no usable latest version (null or not a strict version) is left out, because a plugin
    /// with nothing published cannot be installed and must not hide the rest of the page.
    /// </summary>
    /// <param name="wire">The parsed reply.</param>
    /// <param name="baseUri">The registry address, for the icon origin check.</param>
    /// <returns>The plugins, with the installed state not yet set.</returns>
    private static List<RegistryPluginDto> ToPlugins(PluginRegistryPageWire wire, Uri baseUri)
    {
        var plugins = new List<RegistryPluginDto>();
        foreach (var item in wire.Items ?? [])
        {
            var plugin = ToPlugin(item, baseUri);
            if (plugin.LatestVersion.Length > 0)
            {
                plugins.Add(plugin);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Checks one plugin row and turns it into the application type. The registry's free text is cleaned (control
    /// characters removed, trimmed) and capped: the admin panel shows it.
    /// </summary>
    /// <param name="item">The row; <see langword="null"/> is not a plugin.</param>
    /// <param name="baseUri">The registry address, for the icon origin check.</param>
    /// <returns>The plugin; its <c>LatestVersion</c> is empty when the registry sent none or a bad one.</returns>
    /// <exception cref="PluginRegistryException">The row is not a plugin (bad id, no name, unknown pricing).</exception>
    private static RegistryPluginDto ToPlugin(PluginRegistryEntryWire? item, Uri baseUri)
    {
        var id = item?.PluginId ?? item?.Id;
        var name = UntrustedText.Clean(item?.Name, MaxNameLength);
        if (item is null
            || !PluginIdentifier.IsWellFormed(id)
            || name.Length == 0
            || !Enum.TryParse<PluginRegistryPricing>(item.Pricing, ignoreCase: true, out var pricing)
            || !Enum.IsDefined(pricing))
        {
            throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
        }

        var category = Enum.TryParse<PluginRegistryCategory>(item.Category, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : PluginRegistryCategory.Other;

        // An icon is shown by the admin panel, so only an address on the registry's own host is passed on.
        var icon = ToOwnAddress(item.IconUrl, baseUri);
        var latest = PluginVersion.TryParse(item.LatestVersion, out _) ? item.LatestVersion! : string.Empty;

        return new RegistryPluginDto(
            id!,
            name,
            UntrustedText.Clean(item.PublisherName ?? item.Author, MaxNameLength),
            UntrustedText.Clean(item.Description, MaxDescriptionLength),
            icon,
            category,
            latest,
            UntrustedText.Clean(item.SdkVersion, MaxSdkVersionLength),
            item.Verified,
            pricing,
            Distribution: Enum.TryParse<PluginRegistryDistribution>(item.Distribution, ignoreCase: true, out var distribution) && Enum.IsDefined(distribution)
                ? distribution
                : PluginRegistryDistribution.Download,
            PublisherKind: Enum.TryParse<PluginRegistryPublisherKind>(item.PublisherKind, ignoreCase: true, out var kind) && Enum.IsDefined(kind)
                ? kind
                : null,
            Tags: ToTags(item.Tags),
            Downloads: item.Downloads is >= 0 ? item.Downloads : null,
            LatestChangelog: CleanMultiline(item.LatestChangelog, MaxChangelogLength),
            Rating: ToRating(item.Rating));
    }

    /// <summary>
    /// Checks the registry's reviews summary. Only an average between 1 and 5 (a star scale) with at least one review is
    /// kept: anything else is dropped rather than shown, so a rating is never invented.
    /// </summary>
    /// <param name="rating">The summary as sent; <see langword="null"/> when the plugin has no reviews.</param>
    /// <returns>The summary, or <see langword="null"/> when it is absent or out of range.</returns>
    private static RegistryPluginRatingDto? ToRating(PluginRegistryRatingWire? rating)
    {
        return rating is { Average: >= 1 and <= 5 and var average, Count: >= 1 and var count }
            ? new RegistryPluginRatingDto(average, count)
            : null;
    }

    /// <summary>Cleans the registry's tags: each is cleaned and capped, empty and repeated ones are dropped, and at most <see cref="MaxTags"/> are kept.</summary>
    /// <param name="tags">The tags as sent; <see langword="null"/> when absent.</param>
    /// <returns>The usable tags in the registry's order; empty when there are none.</returns>
    private static List<string> ToTags(List<string?>? tags)
    {
        var kept = new List<string>();
        foreach (var raw in tags ?? [])
        {
            var tag = UntrustedText.Clean(raw, MaxTagLength);
            if (tag.Length > 0 && !kept.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                kept.Add(tag);
            }

            if (kept.Count == MaxTags)
            {
                break;
            }
        }

        return kept;
    }

    /// <summary>
    /// Turns the answer of the newest-version lookup (a bare array of version details) into application rows. The
    /// array carries no name, description or category, so the id stands in for the name; a licence requirement reads as
    /// <c>Paid</c>. An item with no usable version is left out.
    /// </summary>
    /// <param name="wire">The parsed array.</param>
    /// <returns>One row per usable item, in the registry's order.</returns>
    private static List<RegistryPluginDto> ToLatest(List<PluginRegistryReleaseWire?> wire)
    {
        var plugins = new List<RegistryPluginDto>();
        foreach (var item in wire)
        {
            if (item is null || !PluginIdentifier.IsWellFormed(item.PluginId))
            {
                throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
            }

            if (!PluginVersion.TryParse(item.Version, out _))
            {
                continue;
            }

            plugins.Add(new RegistryPluginDto(
                item.PluginId!,
                item.PluginId!,
                string.Empty,
                string.Empty,
                null,
                PluginRegistryCategory.Other,
                item.Version!,
                UntrustedText.Clean(item.SdkVersion, MaxSdkVersionLength),
                item.Verified ?? false,
                item.RequiresLicence ?? false ? PluginRegistryPricing.Paid : PluginRegistryPricing.Free,
                LatestChangelog: CleanMultiline(item.Changelog, MaxChangelogLength)));
        }

        return plugins;
    }

    /// <summary>Checks one version row and turns it into the application type.</summary>
    /// <param name="item">The row.</param>
    /// <param name="baseUri">The registry address, for the image origin check.</param>
    /// <returns>The version; <see langword="null"/> when its number or status is not usable.</returns>
    private static RegistryPluginVersion? ToVersion(PluginRegistryVersionWire item, Uri baseUri)
    {
        if (!PluginVersion.TryParse(item.Version, out _)
            || !Enum.TryParse<PluginRegistryVersionStatus>(item.Status, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            return null;
        }

        var permissions = new List<PluginRegistryCapability>();
        foreach (var name in item.Permissions ?? [])
        {
            if (Enum.TryParse<PluginRegistryCapability>(name, ignoreCase: true, out var capability)
                && Enum.IsDefined(capability)
                && !permissions.Contains(capability))
            {
                permissions.Add(capability);
            }
        }

        var screenshots = new List<RegistryPluginImage>();
        foreach (var shot in (item.Screenshots ?? []).Take(MaxScreenshots))
        {
            var image = ToImage(shot, baseUri);
            if (image is not null)
            {
                screenshots.Add(image);
            }
        }

        return new RegistryPluginVersion(
            item.Version!,
            UntrustedText.Clean(item.SdkVersion, MaxSdkVersionLength),
            Math.Max(0, item.SizeBytes),
            item.PublishedAt,
            status,
            CleanOrNull(item.StatusReason, MaxShortTextLength),
            CleanOrNull(item.ReplacementVersion, MaxSdkVersionLength),
            CleanOrNull(item.MinHostVersion, MaxSdkVersionLength),
            CleanOrNull(item.MaxHostVersion, MaxSdkVersionLength),
            CleanMultiline(item.Changelog, MaxChangelogLength),
            permissions,
            ToImage(item.Icon, baseUri),
            screenshots);
    }

    /// <summary>Turns an image row into the application type.</summary>
    /// <param name="image">The row; may be <see langword="null"/>.</param>
    /// <param name="baseUri">The registry address.</param>
    /// <returns>The image, or <see langword="null"/> when there is none or it is not on the registry's origin.</returns>
    private static RegistryPluginImage? ToImage(PluginRegistryImageWire? image, Uri baseUri)
    {
        var url = ToOwnAddress(image?.Url, baseUri);
        return image is null || url is null
            ? null
            : new RegistryPluginImage(url, UntrustedText.Clean(image.ContentType, MaxShortTextLength), PositiveOrNull(image.Width), PositiveOrNull(image.Height));
    }

    /// <summary>Resolves an address against the registry address and keeps it only when it is on the registry's origin.</summary>
    /// <param name="address">The address as the registry sent it, absolute or relative.</param>
    /// <param name="baseUri">The registry address.</param>
    /// <returns>The absolute address, or <see langword="null"/>.</returns>
    private static string? ToOwnAddress(string? address, Uri baseUri)
    {
        return !string.IsNullOrWhiteSpace(address) && Uri.TryCreate(baseUri, address, out var resolved) && SameOrigin(resolved, baseUri)
            ? resolved.AbsoluteUri
            : null;
    }

    /// <summary>Keeps an external link only when it is an absolute http or https address without user info.</summary>
    /// <param name="address">The address as the registry sent it.</param>
    /// <returns>The address, or <see langword="null"/> for a missing, relative, overlong or other-scheme one.</returns>
    private static string? ToWebLink(string? address)
    {
        return !string.IsNullOrWhiteSpace(address)
            && address.Length <= MaxLinkLength
            && Uri.TryCreate(address.Trim(), UriKind.Absolute, out var link)
            && link.Scheme is "http" or "https"
            && string.IsNullOrEmpty(link.UserInfo)
            ? link.AbsoluteUri
            : null;
    }

    /// <summary>Cleans a text and gives <see langword="null"/> for an empty result.</summary>
    /// <param name="value">The outside text.</param>
    /// <param name="maxLength">The most characters kept.</param>
    /// <returns>The cleaned text, or <see langword="null"/>.</returns>
    private static string? CleanOrNull(string? value, int maxLength)
    {
        var cleaned = UntrustedText.Clean(value, maxLength);
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Cleans a text but keeps its line breaks, which a changelog needs.</summary>
    /// <param name="value">The outside text.</param>
    /// <param name="maxLength">The most characters kept.</param>
    /// <returns>The cleaned text with line feed breaks, or <see langword="null"/> when empty.</returns>
    private static string? CleanMultiline(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalised = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var cleaned = new string(normalised.Select(c => char.IsControl(c) && c != '\n' ? ' ' : c).ToArray()).Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].TrimEnd();
    }

    /// <summary>Keeps a pixel size only when it is positive.</summary>
    /// <param name="value">The size.</param>
    /// <returns>The size, or <see langword="null"/>.</returns>
    private static int? PositiveOrNull(int? value)
    {
        return value is > 0 ? value : null;
    }

    /// <summary>
    /// Validates a licence key before it is sent. A blank key means none. A key with a space, a control character or a
    /// non-ASCII character, or one that is too long, cannot be a header value and is refused as invalid without a
    /// request; the key itself is never put in the error.
    /// </summary>
    /// <param name="licenceKey">The key as the caller has it.</param>
    /// <returns>The trimmed key, or <see langword="null"/> when there is none.</returns>
    private static string? NormaliseLicenceKey(string? licenceKey)
    {
        if (string.IsNullOrWhiteSpace(licenceKey))
        {
            return null;
        }

        var key = licenceKey.Trim();
        if (!RegistryLicenceKeyRule.IsValid(key))
        {
            throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_LICENCE_INVALID);
        }

        return key;
    }
}
