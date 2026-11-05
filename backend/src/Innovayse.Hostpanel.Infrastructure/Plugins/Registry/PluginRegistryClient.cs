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

    /// <summary>Logs a failed call by exception type only.</summary>
    private readonly ILogger<PluginRegistryClient> _logger;

    /// <summary>Initializes the client.</summary>
    /// <param name="http">The HTTP client, built with automatic redirects off.</param>
    /// <param name="options">The registry configuration.</param>
    /// <param name="pluginOptions">The plugin folders, for the staging folder.</param>
    /// <param name="logger">Logs a failed call by exception type only.</param>
    public PluginRegistryClient(
        HttpClient http,
        IOptions<PluginRegistryOptions> options,
        IOptions<PluginOptions> pluginOptions,
        ILogger<PluginRegistryClient> logger)
    {
        _http = http;
        _options = options.Value;
        _pluginOptions = pluginOptions.Value;
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
            var wire = await ReadJsonAsync<PluginRegistryPageWire>(response, token);
            return ToPlugins(wire, baseUri);
        }, ct);
    }

    /// <inheritdoc/>
    public Task<Result<RegistryPluginRelease>> GetReleaseAsync(string pluginId, string version, CancellationToken ct)
    {
        return RunAsync(async (baseUri, token) =>
        {
            // The id and version have the validated shape, and are escaped all the same: they become path segments.
            // A 404 here, and only here, means the registry does not list that version.
            var url = new Uri(baseUri, $"plugins/{Uri.EscapeDataString(pluginId)}/versions/{Uri.EscapeDataString(version)}");
            var wire = await GetJsonAsync<PluginRegistryReleaseWire>(url, baseUri, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND, token);

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
                wire.PluginId ?? pluginId, wire.Version ?? version, download, wire.Sha256.ToLowerInvariant(), wire.Verified);
        }, ct);
    }

    /// <inheritdoc/>
    public Task<Result<Stream>> DownloadAsync(RegistryPluginRelease release, CancellationToken ct)
    {
        return RunAsync<Stream>(async (baseUri, token) =>
        {
            if (!SameOrigin(release.DownloadUrl, baseUri))
            {
                throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, release.DownloadUrl);
            using var response = await SendAsync(request, baseUri, AcceptPackage, HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE, token);

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
    /// <returns>The parsed reply.</returns>
    private async Task<T> GetJsonAsync<T>(Uri url, Uri baseUri, string notFoundCode, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, baseUri, AcceptJson, notFoundCode, ct);
        return await ReadJsonAsync<T>(response, ct);
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
    /// <returns>A successful response; the caller disposes it.</returns>
    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, Uri baseUri, string accept, string notFoundCode, CancellationToken ct)
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
                var response = await _http.SendAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var status = response.StatusCode;
                var location = response.Headers.Location;
                response.Dispose();

                if (status == HttpStatusCode.NotFound)
                {
                    throw new PluginRegistryException(notFoundCode);
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
    /// Checks every listed plugin and turns it into the application type; one bad entry fails the whole reply. The
    /// registry's free text is cleaned (control characters removed, trimmed) and capped: the admin panel shows it.
    /// </summary>
    /// <param name="wire">The parsed reply.</param>
    /// <param name="baseUri">The registry address, for the icon origin check.</param>
    /// <returns>The plugins, with the installed state not yet set.</returns>
    private static List<RegistryPluginDto> ToPlugins(PluginRegistryPageWire wire, Uri baseUri)
    {
        var plugins = new List<RegistryPluginDto>();
        foreach (var item in wire.Items ?? [])
        {
            var name = UntrustedText.Clean(item?.Name, MaxNameLength);
            if (item is null
                || !PluginIdentifier.IsWellFormed(item.Id)
                || name.Length == 0
                || !PluginVersion.TryParse(item.LatestVersion, out _)
                || !Enum.TryParse<PluginRegistryPricing>(item.Pricing, ignoreCase: true, out var pricing)
                || !Enum.IsDefined(pricing))
            {
                throw new PluginRegistryException(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
            }

            var category = Enum.TryParse<PluginRegistryCategory>(item.Category, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : PluginRegistryCategory.Other;

            // An icon is shown by the admin panel, so only an address on the registry's own host is passed on.
            var icon = Uri.TryCreate(baseUri, item.IconUrl, out var iconUri) && SameOrigin(iconUri, baseUri) ? iconUri.AbsoluteUri : null;

            plugins.Add(new RegistryPluginDto(
                item.Id!,
                name,
                UntrustedText.Clean(item.Author, MaxNameLength),
                UntrustedText.Clean(item.Description, MaxDescriptionLength),
                icon,
                category,
                item.LatestVersion!,
                UntrustedText.Clean(item.SdkVersion, MaxSdkVersionLength),
                item.Verified,
                pricing));
        }

        return plugins;
    }
}
