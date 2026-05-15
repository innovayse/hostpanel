namespace Innovayse.Providers.CWP.Models;

/// <summary>Parameters for a CWP account operation sent as form fields.</summary>
internal sealed class CwpAccountRequest
{
    /// <summary>Gets or initializes the CWP API key for authentication.</summary>
    public required string Key { get; init; }

    /// <summary>Gets or initializes the action to perform (add, susp, unsp, del, detail).</summary>
    public required string Action { get; init; }

    /// <summary>Gets or initializes the primary domain for the account.</summary>
    public string? Domain { get; init; }

    /// <summary>Gets or initializes the cPanel username for the account.</summary>
    public string? User { get; init; }

    /// <summary>Gets or initializes the account password (used only on creation).</summary>
    public string? Pass { get; init; }

    /// <summary>Gets or initializes the hosting package name assigned to the account.</summary>
    public string? Package { get; init; }

    /// <summary>Gets or initializes the contact email address for the account.</summary>
    public string? Email { get; init; }

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

        return new FormUrlEncodedContent(fields);
    }
}
