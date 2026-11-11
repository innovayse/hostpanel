using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A plugin registry as an HTTP server the host talks to over its real client: instances register and verify (the registry fetches
/// the challenge from the address of the installation, here through a function the test sets), and a plugin that is host only is
/// given only to a verified instance whose signed call checks out, with a one time ticket for the package. The server follows the
/// registry's rules, written independently of the host's code: it builds the signed text itself, checks the Ed25519 signature
/// with the key the instance registered, the five minute window and the nonce, and answers with the registry's error codes. It
/// fakes the remote server only, never our code.
/// </summary>
public sealed class FakeRegistryServer : HttpMessageHandler
{
    /// <summary>The registry address the tests configure on the host.</summary>
    public const string BaseUrl = "https://registry.example.test/v1/";

    /// <summary>The path every registry route sits under.</summary>
    private const string PathPrefix = "/v1/";

    /// <summary>How far a timestamp may be from the registry clock, either way.</summary>
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    /// <summary>The JSON conventions of the registry: camelCase.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Guards the state below: the host may call from several threads.</summary>
    private readonly object _gate = new();

    /// <summary>The instances by id.</summary>
    private readonly Dictionary<Guid, FakeRegistryInstance> _instances = [];

    /// <summary>The plugin versions by plugin id and version.</summary>
    private readonly Dictionary<(string Id, string Version), FakeRegistryPlugin> _plugins = [];

    /// <summary>The tickets issued: token to the instance, plugin and version it is bound to, and whether it was used.</summary>
    private readonly Dictionary<string, (Guid Instance, string Id, string Version, bool Used)> _tickets = [];

    /// <summary>Every request received, in order.</summary>
    private readonly List<RecordedRegistryCall> _calls = [];

    /// <summary>Gets or sets how the registry reads the challenge from the address of an installation; the test points it at the host.</summary>
    public Func<Guid, Task<string?>> FetchChallenge { get; set; } = _ => Task.FromResult<string?>(null);

    /// <summary>Gets or sets a function that may answer a request before the server does, for a refusal the test wants to force.</summary>
    public Func<RecordedRegistryCall, HttpResponseMessage?>? Intercept { get; set; }

    /// <summary>Gets or sets whether the next verification answers that the challenge ran out of time, once.</summary>
    public bool ExpireNextChallenge { get; set; }

    /// <summary>Gets or sets how far the registry clock is from the host clock, to prove the window is enforced.</summary>
    public TimeSpan ClockOffset { get; set; } = TimeSpan.Zero;

    /// <summary>Gets a copy of every request received so far.</summary>
    public IReadOnlyList<RecordedRegistryCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Gets a copy of the instances registered so far.</summary>
    public IReadOnlyList<FakeRegistryInstance> Instances
    {
        get
        {
            lock (_gate)
            {
                return [.. _instances.Values];
            }
        }
    }

    /// <summary>Offers a plugin version.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="version">The version.</param>
    /// <param name="package">The package bytes.</param>
    /// <param name="hostOnly">Whether only verified installations may have it.</param>
    /// <param name="publishedSha256">The checksum to publish; the real one when <see langword="null"/>.</param>
    public void Offer(string pluginId, string version, byte[] package, bool hostOnly, string? publishedSha256 = null)
    {
        lock (_gate)
        {
            _plugins[(pluginId, version)] = new FakeRegistryPlugin
            {
                PluginId = pluginId,
                Version = version,
                Package = package,
                HostOnly = hostOnly,
                Sha256 = publishedSha256 ?? Convert.ToHexStringLower(SHA256.HashData(package)),
            };
        }
    }

    /// <summary>Revokes an instance, as the registry's administrator would.</summary>
    /// <param name="id">The instance id.</param>
    public void Revoke(Guid id)
    {
        lock (_gate)
        {
            _instances[id].Revoked = true;
        }
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var target = request.RequestUri!.PathAndQuery;
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var call = new RecordedRegistryCall(request.Method.Method, target, headers, body);
        lock (_gate)
        {
            _calls.Add(call);
        }

        if (Intercept?.Invoke(call) is { } forced)
        {
            return forced;
        }

        var path = request.RequestUri.AbsolutePath;
        if (!path.StartsWith(PathPrefix, StringComparison.Ordinal))
        {
            return Problem(HttpStatusCode.NotFound, "NOT_FOUND");
        }

        var route = path[PathPrefix.Length..].Trim('/').Split('/');
        return (request.Method.Method, route) switch
        {
            ("POST", ["instances"]) => Register(body),
            ("POST", ["instances", var id, "verify"]) when Guid.TryParse(id, out var instanceId) => await VerifyAsync(instanceId),
            ("POST", ["plugins", "latest"]) => Latest(),
            ("GET", ["plugins"]) => Listing(),
            ("GET", ["plugins", var id]) => Detail(id),
            ("GET", ["plugins", var id, "versions"]) => Versions(call, id),
            ("GET", ["plugins", var id, "versions", var version]) => Version(call, id, version),
            ("POST", ["plugins", var id, "versions", var version, "download-tickets"]) => Ticket(call, id, version),
            ("GET", ["plugins", var id, "versions", var version, "download"]) => Download(call, id, version),
            _ => Problem(HttpStatusCode.NotFound, "NOT_FOUND"),
        };
    }

    /// <summary>Builds a registry error reply.</summary>
    /// <param name="status">The status.</param>
    /// <param name="code">The registry error code, put in <c>type</c>.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage Problem(HttpStatusCode status, string code)
    {
        var body = JsonSerializer.Serialize(new { type = code, title = code, status = (int)status, detail = code }, Json);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/problem+json") };
    }

    /// <summary>Builds a JSON reply.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="status">The status.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage Reply(object value, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json") };
    }

    /// <summary>Registers an instance: a new id and challenge for a key the registry has not seen.</summary>
    /// <param name="body">The request body.</param>
    /// <returns>201 with the registration, 409 for a known key, 422 for a bad key or address.</returns>
    private HttpResponseMessage Register(string body)
    {
        using var document = JsonDocument.Parse(body);
        var publicKey = Convert.FromBase64String(document.RootElement.GetProperty("publicKey").GetString()!);
        var baseUrl = document.RootElement.GetProperty("baseUrl").GetString()!;
        if (publicKey.Length != 32 || !baseUrl.StartsWith("https://", StringComparison.Ordinal))
        {
            return Problem(HttpStatusCode.UnprocessableEntity, "INSTANCE_VALIDATION_FAILED");
        }

        lock (_gate)
        {
            if (_instances.Values.Any(i => i.PublicKey.AsSpan().SequenceEqual(publicKey)))
            {
                return Problem(HttpStatusCode.Conflict, "INSTANCE_KEY_ALREADY_REGISTERED");
            }

            var instance = new FakeRegistryInstance
            {
                Id = Guid.NewGuid(),
                PublicKey = publicKey,
                BaseUrl = baseUrl.TrimEnd('/'),
                Challenge = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
            };
            _instances[instance.Id] = instance;
            return Reply(
                new
                {
                    id = instance.Id,
                    state = "Pending",
                    challenge = instance.Challenge,
                    challengePath = $"/.well-known/hostpanel-registry/{instance.Id:D}",
                    challengeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                },
                HttpStatusCode.Created);
        }
    }

    /// <summary>Verifies an instance: reads the challenge from its address and compares it.</summary>
    /// <param name="id">The instance id.</param>
    /// <returns>200 when the challenge matches, 404 for an unknown id, 409 when not pending, 422 otherwise.</returns>
    private async Task<HttpResponseMessage> VerifyAsync(Guid id)
    {
        FakeRegistryInstance? instance;
        lock (_gate)
        {
            if (!_instances.TryGetValue(id, out instance))
            {
                return Problem(HttpStatusCode.NotFound, "INSTANCE_NOT_FOUND");
            }

            if (instance.Verified || instance.Revoked)
            {
                return Problem(HttpStatusCode.Conflict, "INSTANCE_STATE_CONFLICT");
            }

            if (ExpireNextChallenge)
            {
                ExpireNextChallenge = false;
                return Problem(HttpStatusCode.UnprocessableEntity, "INSTANCE_CHALLENGE_VALIDATION_FAILED");
            }

            instance.VerificationAttempts++;
        }

        var served = await FetchChallenge(id);
        lock (_gate)
        {
            if (served is null || !string.Equals(served.Trim(), instance.Challenge, StringComparison.Ordinal))
            {
                return Problem(HttpStatusCode.UnprocessableEntity, "INSTANCE_PROOF_VALIDATION_FAILED");
            }

            instance.Verified = true;
            return Reply(new { id = instance.Id, state = "Verified", baseUrl = instance.BaseUrl });
        }
    }

    /// <summary>Checks the signature headers of a call the way the registry does.</summary>
    /// <param name="call">The request.</param>
    /// <param name="instance">The authenticated instance when the call passes.</param>
    /// <returns><see langword="null"/> when the call passes; the refusal otherwise.</returns>
    private HttpResponseMessage? Authenticate(RecordedRegistryCall call, out FakeRegistryInstance? instance)
    {
        instance = null;
        var h = call.Headers;
        if (!h.TryGetValue("X-Hostpanel-Instance", out var rawId)
            || !Guid.TryParse(rawId, out var id)
            || !h.TryGetValue("X-Hostpanel-Timestamp", out var timestamp)
            || !long.TryParse(timestamp, out var seconds)
            || !h.TryGetValue("X-Hostpanel-Nonce", out var nonce)
            || nonce.Length is < 16 or > 64
            || !h.TryGetValue("X-Hostpanel-Signature", out var rawSignature))
        {
            return Problem(HttpStatusCode.Unauthorized, "INSTANCE_SIGNATURE_INVALID_UNAUTHORIZED");
        }

        lock (_gate)
        {
            if (!_instances.TryGetValue(id, out var known))
            {
                return Problem(HttpStatusCode.Unauthorized, "INSTANCE_SIGNATURE_INVALID_UNAUTHORIZED");
            }

            // The registry's text: tag, method, target as received, hash of the (empty) body, timestamp, nonce, one per line.
            var emptyBody = Convert.ToHexStringLower(SHA256.HashData([]));
            var text = string.Join('\n', "HOSTPANEL-REGISTRY-V1", call.Method, call.Target, emptyBody, timestamp, nonce);
            var verifier = new Ed25519Signer();
            verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(known.PublicKey, 0));
            var bytes = Encoding.UTF8.GetBytes(text);
            verifier.BlockUpdate(bytes, 0, bytes.Length);
            if (!TryDecode(rawSignature, out var signature) || !verifier.VerifySignature(signature))
            {
                return Problem(HttpStatusCode.Unauthorized, "INSTANCE_SIGNATURE_INVALID_UNAUTHORIZED");
            }

            if (known.Revoked)
            {
                return Problem(HttpStatusCode.Forbidden, "INSTANCE_REVOKED_FORBIDDEN");
            }

            if (!known.Verified)
            {
                return Problem(HttpStatusCode.Forbidden, "INSTANCE_NOT_VERIFIED_FORBIDDEN");
            }

            var distance = (DateTimeOffset.UtcNow + ClockOffset - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration();
            if (distance > Skew)
            {
                return Problem(HttpStatusCode.Unauthorized, "INSTANCE_TIMESTAMP_UNAUTHORIZED");
            }

            if (!known.Nonces.Add(nonce))
            {
                return Problem(HttpStatusCode.Unauthorized, "INSTANCE_REPLAY_UNAUTHORIZED");
            }

            instance = known;
            return null;
        }
    }

    /// <summary>Decodes a 64-byte base64 signature.</summary>
    /// <param name="text">The header text.</param>
    /// <param name="signature">The bytes when valid.</param>
    /// <returns><see langword="true"/> for a signature-sized base64 value.</returns>
    private static bool TryDecode(string text, out byte[] signature)
    {
        var buffer = new byte[67];
        if (Convert.TryFromBase64String(text, buffer, out var written) && written == 64)
        {
            signature = buffer[..written];
            return true;
        }

        signature = [];
        return false;
    }

    /// <summary>Finds a plugin version; a host only plugin is admitted only to a signed call that passes.</summary>
    /// <param name="call">The request.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="version">The version.</param>
    /// <param name="refusal">The refusal when the call may not see it.</param>
    /// <param name="instance">The authenticated instance, for a host only plugin.</param>
    /// <returns>The plugin version, or <see langword="null"/> with <paramref name="refusal"/> set.</returns>
    private FakeRegistryPlugin? Admit(RecordedRegistryCall call, string id, string version, out HttpResponseMessage? refusal, out FakeRegistryInstance? instance)
    {
        instance = null;
        FakeRegistryPlugin? plugin;
        lock (_gate)
        {
            _plugins.TryGetValue((id, version), out plugin);
            plugin ??= _plugins.Values.Where(p => p.PluginId == id).OrderByDescending(p => p.Version, StringComparer.Ordinal).FirstOrDefault(_ => version == "latest");
        }

        if (plugin is null)
        {
            refusal = Problem(HttpStatusCode.NotFound, "PLUGIN_VERSION_NOT_FOUND");
            return null;
        }

        if (plugin.HostOnly)
        {
            // A host only plugin does not exist for a call that is not signed.
            refusal = call.IsSigned ? Authenticate(call, out instance) : Problem(HttpStatusCode.NotFound, "PLUGIN_NOT_FOUND");
            return refusal is null ? plugin : null;
        }

        refusal = null;
        return plugin;
    }

    /// <summary>The public listing: one row per offered plugin, each with its distribution.</summary>
    /// <returns>200 with the page.</returns>
    private HttpResponseMessage Listing()
    {
        lock (_gate)
        {
            var rows = _plugins.Values
                .GroupBy(p => p.PluginId)
                .Select(g => g.OrderByDescending(p => p.Version, StringComparer.Ordinal).First())
                .Select(p => new
                {
                    pluginId = p.PluginId,
                    name = $"Plugin {p.PluginId}",
                    description = "A test plugin.",
                    publisherName = "Test",
                    category = "Other",
                    latestVersion = p.Version,
                    verified = true,
                    pricing = "Free",
                    distribution = p.HostOnly ? "HostOnly" : "Download",
                })
                .ToList();
            return Reply(new { items = rows, total = rows.Count });
        }
    }

    /// <summary>The page of a plugin: public, with its distribution.</summary>
    /// <param name="id">The plugin id.</param>
    /// <returns>200 with the page, 404 for an unknown plugin.</returns>
    private HttpResponseMessage Detail(string id)
    {
        FakeRegistryPlugin? latest;
        lock (_gate)
        {
            latest = _plugins.Values.Where(p => p.PluginId == id).OrderByDescending(p => p.Version, StringComparer.Ordinal).FirstOrDefault();
        }

        return latest is null
            ? Problem(HttpStatusCode.NotFound, "PLUGIN_NOT_FOUND")
            : Reply(new
            {
                summary = new
                {
                    pluginId = id,
                    name = $"Plugin {id}",
                    description = "A test plugin.",
                    publisherName = "Test",
                    category = "Other",
                    latestVersion = latest.Version,
                    verified = true,
                    pricing = "Free",
                    distribution = latest.HostOnly ? "HostOnly" : "Download",
                },
            });
    }

    /// <summary>The version list of a plugin.</summary>
    /// <param name="call">The request.</param>
    /// <param name="id">The plugin id.</param>
    /// <returns>200 with the versions, or the refusal.</returns>
    private HttpResponseMessage Versions(RecordedRegistryCall call, string id)
    {
        List<FakeRegistryPlugin> own;
        lock (_gate)
        {
            own = [.. _plugins.Values.Where(p => p.PluginId == id)];
        }

        if (own.Count == 0)
        {
            return Problem(HttpStatusCode.NotFound, "PLUGIN_NOT_FOUND");
        }

        var first = Admit(call, id, own[0].Version, out var refusal, out _);
        return first is null
            ? refusal!
            : Reply(own.Select(p => new
            {
                version = p.Version,
                sdkVersion = "3.1.0",
                sizeBytes = p.Package.Length,
                publishedAt = DateTimeOffset.UtcNow,
                status = "Published",
            }));
    }

    /// <summary>One version of a plugin: its package address and checksum.</summary>
    /// <param name="call">The request.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="version">The version.</param>
    /// <returns>200 with the release, or the refusal.</returns>
    private HttpResponseMessage Version(RecordedRegistryCall call, string id, string version)
    {
        var plugin = Admit(call, id, version, out var refusal, out _);
        return plugin is null
            ? refusal!
            : Reply(new
            {
                pluginId = id,
                version = plugin.Version,
                downloadUrl = $"{BaseUrl}plugins/{id}/versions/{plugin.Version}/download",
                sha256 = plugin.Sha256,
                verified = true,
                sdkVersion = "3.1.0",
                requiresLicence = false,
            });
    }

    /// <summary>Issues a one time ticket to a verified instance for a host only plugin.</summary>
    /// <param name="call">The request.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="version">The version, or <c>latest</c>.</param>
    /// <returns>200 with the ticket, or the refusal.</returns>
    private HttpResponseMessage Ticket(RecordedRegistryCall call, string id, string version)
    {
        var plugin = Admit(call, id, version, out var refusal, out var instance);
        if (plugin is null)
        {
            return refusal!;
        }

        if (!plugin.HostOnly)
        {
            return Problem(HttpStatusCode.NotFound, "PLUGIN_NOT_FOUND");
        }

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        lock (_gate)
        {
            _tickets[token] = (instance!.Id, id, plugin.Version, false);
        }

        return Reply(new
        {
            ticket = token,
            downloadUrl = $"{BaseUrl}plugins/{id}/versions/{plugin.Version}/download?ticket={token}",
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(60),
        });
    }

    /// <summary>Serves a package: a public one freely, a host only one against a ticket issued to the signing instance, once.</summary>
    /// <param name="call">The request.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="version">The version.</param>
    /// <returns>200 with the zip, or the refusal.</returns>
    private HttpResponseMessage Download(RecordedRegistryCall call, string id, string version)
    {
        var plugin = Admit(call, id, version, out var refusal, out var instance);
        if (plugin is null)
        {
            return refusal!;
        }

        if (plugin.HostOnly)
        {
            var token = System.Web.HttpUtility.ParseQueryString(call.Target[(call.Target.IndexOf('?') + 1)..])["ticket"];
            lock (_gate)
            {
                if (token is null
                    || !_tickets.TryGetValue(token, out var ticket)
                    || ticket.Used
                    || ticket.Instance != instance!.Id
                    || ticket.Id != id
                    || ticket.Version != plugin.Version)
                {
                    return Problem(HttpStatusCode.Forbidden, "DOWNLOAD_TICKET_INVALID_FORBIDDEN");
                }

                _tickets[token] = ticket with { Used = true };
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(plugin.Package) { Headers = { ContentType = new("application/zip") } } };
    }

    /// <summary>The newest-version lookup: unsigned, with no package address for a host only plugin.</summary>
    /// <returns>200 with one row per offered plugin.</returns>
    private HttpResponseMessage Latest()
    {
        lock (_gate)
        {
            return Reply(_plugins.Values
                .GroupBy(p => p.PluginId)
                .Select(g => g.OrderByDescending(p => p.Version, StringComparer.Ordinal).First())
                .Select(p => new
                {
                    pluginId = p.PluginId,
                    version = p.Version,
                    downloadUrl = p.HostOnly ? string.Empty : $"{BaseUrl}plugins/{p.PluginId}/versions/{p.Version}/download",
                    sha256 = p.Sha256,
                    verified = true,
                    sdkVersion = "3.1.0",
                    requiresLicence = false,
                }));
        }
    }
}
