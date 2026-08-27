namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Local-mode two-factor authentication: the account, secret and codes all live in this
/// product's own database, reached through the existing <see cref="IUserService"/>.
///
/// <para>
/// A thin delegate rather than a place that owns the TOTP logic itself, so as not to touch
/// <see cref="IUserService"/>'s other 15+ unrelated members or its one implementation for a
/// fix scoped to three handlers. The Issuer label and enrolment URI shape used to live on
/// <see cref="Innovayse.Application.Auth.Commands.StartTwoFactorSetup.StartTwoFactorSetupHandler"/>
/// directly; it moves here because building that URI is TOTP-specific, not something either
/// mode's handler should still know about once the handler only calls this interface.
/// </para>
/// </summary>
/// <param name="users">Identity access — the local implementation this delegates every call to.</param>
/// <param name="configuration">
/// Reads <c>AppName</c> for the label an authenticator app shows. Local mode is what a
/// self-hosted, third-party deployment uses — one with no Innovayse SSO of its own — so the
/// name burned into every enrolled account's QR code must be that deployment's own, not this
/// product's. Defaulting to "Innovayse" costs the platform's own deployment nothing, since it
/// never has to set the key at all.
/// </param>
public sealed class LocalTwoFactorService(IUserService users, IConfiguration configuration) : ITwoFactorService
{
    /// <summary>The name shown in the authenticator app beside the account.</summary>
    private string Issuer => configuration["AppName"] is { Length: > 0 } name ? name : "Innovayse";

    /// <inheritdoc/>
    public async Task<TwoFactorSetupDto?> StartSetupAsync(string userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null) return null;

        var secret = await users.GenerateTwoFactorSecretAsync(userId, ct);

        // Both label halves are escaped: an address with a colon or a space in it would
        // otherwise split the label and produce a URI the app reads as a different account.
        var label = $"{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(user.Value.Email)}";
        var uri = $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}";

        return new TwoFactorSetupDto(secret, uri);
    }

    /// <inheritdoc/>
    public async Task<bool> EnableAsync(string userId, string code, CancellationToken ct)
    {
        if (!await users.VerifyTwoFactorCodeAsync(userId, code, ct)) return false;

        await users.EnableTwoFactorAsync(userId, ct);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DisableAsync(string userId, string code, CancellationToken ct)
    {
        if (!await users.VerifyTwoFactorCodeAsync(userId, code, ct)) return false;

        await users.DisableTwoFactorAsync(userId, ct);
        return true;
    }
}
