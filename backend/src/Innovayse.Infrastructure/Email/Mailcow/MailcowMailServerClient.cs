namespace Innovayse.Infrastructure.Email.Mailcow;

using System.Text.Json;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Mailcow implementation of <see cref="IMailServerClient"/>.
/// Translates domain-level mail server operations into Mailcow REST API calls
/// via the typed <see cref="MailcowClient"/>.
/// </summary>
public sealed class MailcowMailServerClient(MailcowClient client) : IMailServerClient
{
    /// <inheritdoc/>
    public async Task<string> CreateDomainAsync(string domain, int maxQuotaMb, int maxMailboxes, CancellationToken ct)
    {
        await client.PostAsync("/api/v1/add/domain", new
        {
            domain,
            active = 1,
            aliases = 100,
            mailboxes = maxMailboxes,
            defquota = maxMailboxes > 0 ? maxQuotaMb / maxMailboxes : maxQuotaMb,
            maxquota = maxQuotaMb,
            quota = maxQuotaMb,
            description = "Managed by Innovayse Hostpanel",
        }, ct);

        return domain;
    }

    /// <inheritdoc/>
    public async Task DeleteDomainAsync(string domain, CancellationToken ct)
    {
        await client.PostAsync("/api/v1/delete/domain", new[] { domain }, ct);
    }

    /// <inheritdoc/>
    public async Task<string?> GenerateDkimAsync(string domain, int keySize, CancellationToken ct)
    {
        await client.PostAsync("/api/v1/add/dkim", new
        {
            domains = domain,
            dkim_selector = "dkim",
            key_size = keySize,
        }, ct);

        // Fetch the generated DKIM public key
        var result = await client.GetAsync($"/api/v1/get/dkim/{domain}", ct);

        if (result.TryGetProperty("dkim_txt", out var dkimTxt))
            return dkimTxt.GetString();

        return null;
    }

    /// <inheritdoc/>
    public async Task CreateMailboxAsync(string email, string password, string displayName, int quotaMb, CancellationToken ct)
    {
        var atIndex = email.IndexOf('@');
        var localPart = atIndex >= 0 ? email[..atIndex] : email;
        var domainPart = atIndex >= 0 ? email[(atIndex + 1)..] : string.Empty;

        await client.PostAsync("/api/v1/add/mailbox", new
        {
            local_part = localPart,
            domain = domainPart,
            name = displayName,
            password,
            password2 = password,
            quota = quotaMb,
            active = 1,
            force_pw_update = 0,
            tls_enforce_in = 0,
            tls_enforce_out = 0,
        }, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteMailboxAsync(string email, CancellationToken ct)
    {
        await client.PostAsync("/api/v1/delete/mailbox", new[] { email }, ct);
    }

    /// <inheritdoc/>
    public async Task UpdateMailboxPasswordAsync(string email, string newPassword, CancellationToken ct)
    {
        await client.PostAsync("/api/v1/edit/mailbox", new
        {
            items = new[] { email },
            attr = new { password = newPassword, password2 = newPassword },
        }, ct);
    }

    /// <inheritdoc/>
    public async Task CreateAliasAsync(string source, string destination, CancellationToken ct)
    {
        await client.PostAsync("/api/v1/add/alias", new
        {
            address = source,
            @goto = destination,
            active = 1,
        }, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAliasAsync(int aliasId, CancellationToken ct)
    {
        await client.PostAsync("/api/v1/delete/alias", new[] { aliasId.ToString() }, ct);
    }
}
