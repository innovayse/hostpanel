using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// The install record of one plugin an operator uploaded or fetched from the registry: when it was first installed and by
/// whom, when a package last replaced it, and what the latest accepted package was (its source, signer, size and digest).
/// Plugins that ship with the build have no record, and neither has one installed before the record existed.
/// </summary>
/// <remarks>
/// <see cref="InstalledAt"/> and <see cref="UpdatedAt"/> are passed in by the caller (the domain reads no clock). An
/// upgrade is staged until the next restart, so the package facts describe the most recently accepted package, which
/// is the running one once the restart has applied it. The record raises no domain event: nothing reacts to an install, and
/// the audit line written in the same save as the record is the trail of who installed what (see "Install records" in the
/// product rules).
/// </remarks>
public sealed class InstalledPlugin : BaseEntity, IAggregateRoot
{
    /// <summary>The column length of a user id or a user name written to the record.</summary>
    public const int UserLength = 256;

    /// <summary>The column length of a signing key id.</summary>
    public const int SignerKeyIdLength = 64;

    /// <summary>The column length of a signing public key: the base64 of a 32-byte Ed25519 key.</summary>
    public const int SignerPublicKeyLength = 44;

    /// <summary>The length of a lower-case hex SHA-256.</summary>
    public const int Sha256Length = 64;

    /// <summary>Gets the plugin id; one record per plugin.</summary>
    public string PluginId { get; private set; } = string.Empty;

    /// <summary>Gets where the latest accepted package came from.</summary>
    public PluginSource Source { get; private set; }

    /// <summary>Gets who vouched for the latest accepted package.</summary>
    public PluginSignerKind Signer { get; private set; }

    /// <summary>Gets the id of the key that signed the latest accepted package; <see langword="null"/> when it is unsigned.</summary>
    public string? SignerKeyId { get; private set; }

    /// <summary>
    /// Gets the public key the registry published for <see cref="SignerKeyId"/> when the package was installed from the
    /// registry and the host did not already trust that key itself (base64 of 32 bytes); <see langword="null"/> otherwise.
    /// The loader trusts the package's signature through it, so the plugin still loads after a restart.
    /// </summary>
    public string? SignerPublicKey { get; private set; }

    /// <summary>Gets the size in bytes of the latest accepted archive.</summary>
    public long PackageSizeBytes { get; private set; }

    /// <summary>Gets the lower-case hex SHA-256 of the latest accepted archive.</summary>
    public string PackageSha256 { get; private set; } = string.Empty;

    /// <summary>Gets when the plugin was first installed.</summary>
    public DateTimeOffset InstalledAt { get; private set; }

    /// <summary>Gets when a package last replaced the first one; <see langword="null"/> while none has.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Gets the subject id of the administrator who first installed the plugin; <see langword="null"/> when unknown.</summary>
    public string? InstalledByUserId { get; private set; }

    /// <summary>Gets the display name of the administrator who first installed the plugin, as it was then; <see langword="null"/> when unknown.</summary>
    public string? InstalledByName { get; private set; }

    /// <summary>EF Core materialisation only.</summary>
    private InstalledPlugin()
    {
    }

    /// <summary>Records the first install of a plugin. The only way a record comes into existence.</summary>
    /// <param name="pluginId">The plugin id; not empty. The plugin id rule is checked by the use case.</param>
    /// <param name="package">What is known about the accepted package.</param>
    /// <param name="installedByUserId">The administrator's subject id; <see langword="null"/> when unknown.</param>
    /// <param name="installedByName">The administrator's display name; <see langword="null"/> when unknown.</param>
    /// <param name="installedAt">The moment of the install.</param>
    /// <returns>The new, unsaved record.</returns>
    /// <exception cref="ArgumentException">The plugin id is empty or the package facts are inconsistent.</exception>
    public static InstalledPlugin Create(
        string pluginId, PluginPackageFacts package, string? installedByUserId, string? installedByName, DateTimeOffset installedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        EnsureConsistent(package);

        var record = new InstalledPlugin
        {
            PluginId = pluginId,
            InstalledAt = installedAt,
            InstalledByUserId = Truncate(installedByUserId, UserLength),
            InstalledByName = Truncate(installedByName, UserLength),
        };
        record.Apply(package);
        return record;
    }

    /// <summary>Records that another package was accepted for the plugin, whether it replaces the running one now or at the next restart.</summary>
    /// <param name="package">What is known about the accepted package.</param>
    /// <param name="updatedAt">The moment the package was accepted.</param>
    /// <exception cref="ArgumentException">The package facts are inconsistent.</exception>
    public void RecordUpload(PluginPackageFacts package, DateTimeOffset updatedAt)
    {
        EnsureConsistent(package);

        Apply(package);
        UpdatedAt = updatedAt;
    }

    /// <summary>Copies the package facts onto the record.</summary>
    /// <param name="package">The facts, already checked by <see cref="EnsureConsistent"/>.</param>
    private void Apply(PluginPackageFacts package)
    {
        Source = package.Source;
        Signer = package.Signer;
        SignerKeyId = package.SignerKeyId;
        SignerPublicKey = package.SignerPublicKey;
        PackageSizeBytes = package.SizeBytes;
        PackageSha256 = package.Sha256;
    }

    /// <summary>Refuses package facts a record cannot hold.</summary>
    /// <param name="package">The facts to check.</param>
    /// <exception cref="ArgumentNullException">The facts are missing.</exception>
    /// <exception cref="ArgumentException">The source is the built-in one, a signed package names no key or an unsigned one names a key, the key id is longer than <see cref="SignerKeyIdLength"/>, a pinned public key is on an unsigned package or too long, or the digest is not a lower-case hex SHA-256.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    private static void EnsureConsistent(PluginPackageFacts package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.Source == PluginSource.BuiltIn)
        {
            throw new ArgumentException("A plugin that ships with the build is not an install record.", nameof(package));
        }

        var named = !string.IsNullOrWhiteSpace(package.SignerKeyId);
        if (named == (package.Signer == PluginSignerKind.Unsigned))
        {
            throw new ArgumentException("A signed package names its key, an unsigned one names none.", nameof(package));
        }

        if (package.SignerKeyId is { } keyId && keyId.Length > SignerKeyIdLength)
        {
            throw new ArgumentException($"The signing key id is longer than {SignerKeyIdLength} characters.", nameof(package));
        }

        if (package.SignerPublicKey is { } publicKey && (!named || publicKey.Length > SignerPublicKeyLength))
        {
            throw new ArgumentException(
                $"A pinned public key belongs to a signed package and is at most {SignerPublicKeyLength} characters.", nameof(package));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(package.SizeBytes);
        if (package.Sha256.Length != Sha256Length || !package.Sha256.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("The digest is not a lower-case hex SHA-256.", nameof(package));
        }
    }

    /// <summary>Cuts a text from the caller to the column length.</summary>
    /// <param name="value">The text; may be <see langword="null"/>.</param>
    /// <param name="length">The column length.</param>
    /// <returns>The text, at most <paramref name="length"/> characters; <see langword="null"/> for a missing or blank text.</returns>
    private static string? Truncate(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= length ? value : value[..length];
    }
}
