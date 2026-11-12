namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>The one rule that says whether an e-mail domain is a listed domain or one of its subdomains.</summary>
public static class EmailDomains
{
    /// <summary>Tells whether a domain is the listed domain or lies under it.</summary>
    /// <param name="domain">The e-mail domain, in lower case.</param>
    /// <param name="listed">The listed domain, in lower case.</param>
    /// <returns><see langword="true"/> for <c>mail.example.com</c> against <c>example.com</c>; <see langword="false"/> for <c>badexample.com</c>.</returns>
    public static bool IsSameOrSubdomain(string domain, string listed)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(listed);

        return domain.Equals(listed, StringComparison.Ordinal) || domain.EndsWith("." + listed, StringComparison.Ordinal);
    }
}
