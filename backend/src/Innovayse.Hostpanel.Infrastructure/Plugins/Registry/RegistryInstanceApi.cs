using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// The two unsigned calls that register this installation with the plugin registry and ask it to confirm the challenge. It
/// talks to the configured registry address only, follows no redirect, reads at most a small bounded reply and reads of an
/// error reply only its error code. What the registry or the network said never reaches a result or a log line: a failure
/// becomes a host error code, and only the exception type is logged.
/// </summary>
public sealed partial class RegistryInstanceApi : IRegistryInstanceApi
{
    /// <summary>The most bytes of a reply read (64 KiB): a registration answer is a few hundred bytes.</summary>
    private const int MaxReplyBytes = 65_536;

    /// <summary>The registry error code of a challenge that ran out of time or attempts, or an instance that is not waiting.</summary>
    private const string ChallengeFailedCode = "INSTANCE_CHALLENGE_VALIDATION_FAILED";

    /// <summary>The longest challenge accepted, in characters; the registry sends 64.</summary>
    private const int MaxChallengeLength = 256;

    /// <summary>The wire's JSON conventions: camelCase names, case-insensitive reads.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The HTTP client. It must not follow redirects.</summary>
    private readonly HttpClient _http;

    /// <summary>The registry configuration.</summary>
    private readonly PluginRegistryOptions _options;

    /// <summary>Logs a failed call by exception type only.</summary>
    private readonly ILogger<RegistryInstanceApi> _logger;

    /// <summary>Initializes the client.</summary>
    /// <param name="http">The HTTP client, built with automatic redirects off.</param>
    /// <param name="options">The registry configuration.</param>
    /// <param name="logger">Logs a failed call by exception type only.</param>
    public RegistryInstanceApi(HttpClient http, IOptions<PluginRegistryOptions> options, ILogger<RegistryInstanceApi> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<RegistryInstanceRegistration>> RegisterAsync(string publicKey, string baseUrl, CancellationToken ct)
    {
        return RunAsync(
            async (registry, token) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(registry, "instances"))
                {
                    Content = JsonContent.Create(new PluginRegistryInstanceBodyWire(publicKey, baseUrl), options: Json),
                };
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                {
                    return Result<RegistryInstanceRegistration>.FailureCode(RegisterFailure(response.StatusCode));
                }

                var wire = await ReadAsync<PluginRegistryInstanceWire>(response, token);
                return wire.Id is { } id && id != Guid.Empty && !string.IsNullOrWhiteSpace(wire.Challenge) && wire.Challenge.Length <= MaxChallengeLength
                    ? Result<RegistryInstanceRegistration>.Success(new RegistryInstanceRegistration(id, wire.Challenge))
                    : Result<RegistryInstanceRegistration>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
            },
            Result<RegistryInstanceRegistration>.FailureCode,
            ct);
    }

    /// <inheritdoc/>
    public Task<Result> VerifyAsync(Guid instanceId, CancellationToken ct)
    {
        return RunAsync(
            async (registry, token) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(registry, $"instances/{instanceId:D}/verify"));
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.IsSuccessStatusCode)
                {
                    return Result.Success();
                }

                var code = response.StatusCode == HttpStatusCode.UnprocessableEntity ? await ErrorCodeAsync(response, token) : null;
                return Result.FailureCode(VerifyFailure(response.StatusCode, code));
            },
            Result.FailureCode,
            ct);
    }

    /// <summary>Gives the host code for a refused registration.</summary>
    /// <param name="status">The registry's status.</param>
    /// <returns>
    /// A fresh start for a conflict (the registry already knows this key), the address code for a refused body, the limit code for
    /// a 429, and unavailable for anything else.
    /// </returns>
    private static string RegisterFailure(HttpStatusCode status)
    {
        return status switch
        {
            HttpStatusCode.Conflict => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID,
            HttpStatusCode.TooManyRequests => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED,
            _ => HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE,
        };
    }

    /// <summary>Gives the host code for a refused verification.</summary>
    /// <param name="status">The registry's status.</param>
    /// <param name="registryCode">The registry error code of a 422, or <see langword="null"/>.</param>
    /// <returns>
    /// A fresh start when the instance is unknown, not waiting, or its challenge ran out of time or attempts; the unreachable
    /// code when the registry could not read the challenge; the limit code for a 429; unavailable for anything else.
    /// </returns>
    private static string VerifyFailure(HttpStatusCode status, string? registryCode)
    {
        return status switch
        {
            HttpStatusCode.NotFound or HttpStatusCode.Conflict => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED,
            HttpStatusCode.UnprocessableEntity when registryCode == ChallengeFailedCode => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED,
            HttpStatusCode.UnprocessableEntity => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE,
            HttpStatusCode.TooManyRequests => HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED,
            _ => HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE,
        };
    }

    /// <summary>Runs one call under the time limit and turns every failure into a result.</summary>
    /// <typeparam name="T">The result type, <see cref="Result"/> or a <see cref="Result{T}"/>.</typeparam>
    /// <param name="operation">The call, given the registry address and the limited token.</param>
    /// <param name="failure">Builds the failed result of the call's type from an error code.</param>
    /// <param name="ct">The caller's token.</param>
    /// <returns>The call's result, or a <c>PLUGIN_REGISTRY_*</c> failure.</returns>
    private async Task<T> RunAsync<T>(Func<Uri, CancellationToken, Task<T>> operation, Func<string, T> failure, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return failure(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        }

        using var limited = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limited.CancelAfter(_options.Timeout);
        try
        {
            var registry = new Uri(_options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/");
            return await operation(registry, limited.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return failure(HostpanelErrorCodes.PLUGIN_REGISTRY_TIMED_OUT);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or InvalidOperationException
            or IOException or UriFormatException)
        {
            // Only the type is logged: the message may carry the address or part of the registry's reply.
            LogCallFailed(ex.GetType().Name);
            return failure(ex is JsonException or NotSupportedException or UriFormatException
                ? HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID
                : HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        }
    }

    /// <summary>Logs that a registry call failed.</summary>
    /// <param name="exceptionType">The name of the exception type.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A plugin registry instance call failed: {ExceptionType}.")]
    private partial void LogCallFailed(string exceptionType);

    /// <summary>Reads a JSON reply under the size limit.</summary>
    /// <typeparam name="T">The wire type.</typeparam>
    /// <param name="response">The successful response.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>The parsed reply; a reply of <c>null</c> is not a reply.</returns>
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await ReadBoundedAsync(response, ct);
        return JsonSerializer.Deserialize<T>(body, Json) ?? throw new JsonException("The reply is empty.");
    }

    /// <summary>Reads the registry error code of an error reply, and nothing else of it.</summary>
    /// <param name="response">The error response.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>The code in <c>type</c>, or <see langword="null"/> when the body has none or is not JSON.</returns>
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return JsonSerializer.Deserialize<PluginRegistryErrorWire>(await ReadBoundedAsync(response, ct), Json)?.Type;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads a body of at most <see cref="MaxReplyBytes"/> bytes.</summary>
    /// <param name="response">The response.</param>
    /// <param name="ct">The limited token.</param>
    /// <returns>The body bytes.</returns>
    /// <exception cref="JsonException">The body is longer than the limit.</exception>
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxReplyBytes)
            {
                throw new JsonException("The reply is too long.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
