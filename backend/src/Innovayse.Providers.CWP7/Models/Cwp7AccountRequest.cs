namespace Innovayse.Providers.CWP7.Models;

/// <summary>Parameters for a CWP7 account operation sent as form fields.</summary>
internal sealed class Cwp7AccountRequest
{
    /// <summary>Gets or initializes the CWP7 API key for authentication.</summary>
    public required string Key { get; init; }

    /// <summary>Gets or initializes the action to perform (add, susp, unsp, del, udp).</summary>
    public required string Action { get; init; }

    /// <summary>Gets or initializes the primary domain for the account.</summary>
    public string? Domain { get; init; }

    /// <summary>Gets or initializes the CWP7 username for the account.</summary>
    public string? User { get; init; }

    /// <summary>Gets or initializes the account password (used only on creation).</summary>
    public string? Pass { get; init; }

    /// <summary>Gets or initializes the hosting package name assigned to the account.</summary>
    public string? Package { get; init; }

    /// <summary>Gets or initializes the contact email address for the account.</summary>
    public string? Email { get; init; }

    /// <summary>Gets or initializes the inode limit (0 for unlimited).</summary>
    public string? Inode { get; init; }

    /// <summary>Gets or initializes the process limit for the account.</summary>
    public string? LimitNproc { get; init; }

    /// <summary>Gets or initializes the open files limit for the account.</summary>
    public string? LimitNofile { get; init; }

    /// <summary>Gets or initializes the server IP address for the account.</summary>
    public string? ServerIps { get; init; }

    /// <summary>Gets or initializes the backup setting (on/off).</summary>
    public string? Backup { get; init; }

    /// <summary>Converts this request into URL-encoded form content for the HTTP POST body.</summary>
    /// <returns>Form content ready to send as the HTTP request body.</returns>
    public FormUrlEncodedContent ToFormContent()
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("key",    Key),
            new("action", Action),
        };

        if (Domain is not null)
        {
            fields.Add(new("domain", Domain));
        }

        if (User is not null)
        {
            fields.Add(new("user", User));
        }

        if (Pass is not null)
        {
            fields.Add(new("pass", Pass));
        }

        if (Package is not null)
        {
            fields.Add(new("package", Package));
        }

        if (Email is not null)
        {
            fields.Add(new("email", Email));
        }

        if (Inode is not null)
        {
            fields.Add(new("inode", Inode));
        }

        if (LimitNproc is not null)
        {
            fields.Add(new("limit_nproc", LimitNproc));
        }

        if (LimitNofile is not null)
        {
            fields.Add(new("limit_nofile", LimitNofile));
        }

        if (ServerIps is not null)
        {
            fields.Add(new("server_ips", ServerIps));
        }

        if (Backup is not null)
        {
            fields.Add(new("banckup", Backup));
        }

        return new FormUrlEncodedContent(fields);
    }
}
