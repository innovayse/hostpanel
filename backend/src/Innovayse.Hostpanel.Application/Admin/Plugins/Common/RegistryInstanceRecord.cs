using System.Text.Json.Serialization;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// This installation's identity towards the plugin registry, and where the registration stands: the key pair, the public
/// address told to the registry, the instance id and challenge it answered with, and the outcome of the last attempt.
/// Stored as one JSON document in an encrypted settings row (<c>PluginSettingKeys.RegistryInstance</c>), because it holds the
/// private key. Every change goes through a method, so a state is never set without the fields it needs.
/// </summary>
public sealed class RegistryInstanceRecord
{
    /// <summary>The raw 32-byte public key, base64; sent to the registry.</summary>
    [JsonInclude]
    public string PublicKey { get; private set; } = string.Empty;

    /// <summary>The raw 32-byte private seed, base64; never sent anywhere.</summary>
    [JsonInclude]
    public string PrivateKey { get; private set; } = string.Empty;

    /// <summary>The public https address told to the registry.</summary>
    [JsonInclude]
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>The instance id the registry gave; <see langword="null"/> before a registration succeeded.</summary>
    [JsonInclude]
    public Guid? InstanceId { get; private set; }

    /// <summary>The text the registry wants served at the well-known address; <see langword="null"/> before a registration succeeded.</summary>
    [JsonInclude]
    public string? Challenge { get; private set; }

    /// <summary>Where the registration stands.</summary>
    [JsonInclude]
    public RegistryInstanceState State { get; private set; } = RegistryInstanceState.NotRegistered;

    /// <summary>The code of the last failed attempt; <see langword="null"/> when the last one did not fail.</summary>
    [JsonInclude]
    public string? ErrorCode { get; private set; }

    /// <summary>How many attempts failed in a row; the back-off grows with it.</summary>
    [JsonInclude]
    public int Failures { get; private set; }

    /// <summary>When the next automatic attempt is due; <see langword="null"/> when nothing waits.</summary>
    [JsonInclude]
    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>When the registry confirmed the installation; <see langword="null"/> until it did.</summary>
    [JsonInclude]
    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>For the JSON reader only.</summary>
    [JsonConstructor]
    private RegistryInstanceRecord()
    {
    }

    /// <summary>Starts an identity with a new key pair, not yet registered.</summary>
    /// <param name="keys">The new key pair.</param>
    /// <param name="baseUrl">The public https address of this installation.</param>
    /// <returns>The record, <see cref="RegistryInstanceState.NotRegistered"/>.</returns>
    public static RegistryInstanceRecord Create(RegistryInstanceKeyPair keys, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        return new RegistryInstanceRecord { PublicKey = keys.PublicKey, PrivateKey = keys.PrivateKey, BaseUrl = baseUrl };
    }

    /// <summary>Whether the automatic attempt is due: nothing waits, or the back-off has run out.</summary>
    /// <param name="now">The current moment.</param>
    /// <returns><see langword="true"/> when an attempt may be made now.</returns>
    public bool IsDue(DateTimeOffset now)
    {
        return NextAttemptAt is null || now >= NextAttemptAt;
    }

    /// <summary>Records a successful registration: the registry gave an id and a challenge to serve.</summary>
    /// <param name="registration">The registry's answer.</param>
    public void Register(RegistryInstanceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        InstanceId = registration.Id;
        Challenge = registration.Challenge;
        State = RegistryInstanceState.Pending;
        ErrorCode = null;
        Failures = 0;
        NextAttemptAt = null;
        VerifiedAt = null;
    }

    /// <summary>Records that the registry confirmed the challenge.</summary>
    /// <param name="now">The current moment.</param>
    public void Verify(DateTimeOffset now)
    {
        State = RegistryInstanceState.Verified;
        ErrorCode = null;
        Failures = 0;
        NextAttemptAt = null;
        VerifiedAt = now;
    }

    /// <summary>Records a failed attempt and sets when the next automatic one is due.</summary>
    /// <param name="errorCode">The code of the failure.</param>
    /// <param name="now">The current moment.</param>
    public void Fail(string errorCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        State = RegistryInstanceState.Error;
        ErrorCode = errorCode;
        Failures++;
        NextAttemptAt = now + RegistryInstanceBackoff.After(Failures);
    }

    /// <summary>Records that the registry revoked this installation. The automatic job leaves it alone from now on.</summary>
    public void Revoke()
    {
        State = RegistryInstanceState.Revoked;
        ErrorCode = null;
        NextAttemptAt = null;
    }

    /// <summary>Records that the registry no longer treats the installation as verified, so the next attempt confirms it again.</summary>
    /// <param name="errorCode">The code that told so.</param>
    public void Unverify(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        State = InstanceId is null ? RegistryInstanceState.NotRegistered : RegistryInstanceState.Pending;
        ErrorCode = errorCode;
        VerifiedAt = null;
        NextAttemptAt = null;
    }

    /// <summary>
    /// Forgets the registration and starts over with a new key pair. The registry refuses to register one key twice, so
    /// registering again always needs a new pair. The failure count and the pause are reset because this is a fresh start.
    /// </summary>
    /// <param name="keys">The new key pair.</param>
    /// <param name="baseUrl">The public https address of this installation, as configured now.</param>
    public void Restart(RegistryInstanceKeyPair keys, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        PublicKey = keys.PublicKey;
        PrivateKey = keys.PrivateKey;
        BaseUrl = baseUrl;
        InstanceId = null;
        Challenge = null;
        State = RegistryInstanceState.NotRegistered;
        ErrorCode = null;
        Failures = 0;
        NextAttemptAt = null;
        VerifiedAt = null;
    }

    /// <summary>Lets the next attempt run at once, without changing anything else.</summary>
    public void ClearPause()
    {
        NextAttemptAt = null;
    }
}
