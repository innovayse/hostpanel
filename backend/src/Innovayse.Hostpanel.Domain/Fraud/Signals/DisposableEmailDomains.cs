namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>
/// The built-in list of disposable mailbox services, kept in this one place. It is a starting point, not a complete census: an
/// operator adds the rest to the block list, and a domain listed here is only a signal, never a refusal on its own.
/// </summary>
public static class DisposableEmailDomains
{
    /// <summary>The disposable mailbox domains, lower case.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "mailinator.com",
        "guerrillamail.com",
        "guerrillamail.net",
        "guerrillamail.org",
        "guerrillamailblock.com",
        "sharklasers.com",
        "grr.la",
        "10minutemail.com",
        "10minutemail.net",
        "tempmail.com",
        "temp-mail.org",
        "temp-mail.io",
        "tempmailo.com",
        "tempail.com",
        "throwawaymail.com",
        "yopmail.com",
        "yopmail.net",
        "trashmail.com",
        "trashmail.net",
        "trash-mail.com",
        "getnada.com",
        "nada.email",
        "dispostable.com",
        "maildrop.cc",
        "mailnesia.com",
        "mintemail.com",
        "mohmal.com",
        "fakeinbox.com",
        "emailondeck.com",
        "spamgourmet.com",
        "mytemp.email",
        "burnermail.io",
        "discard.email",
        "mailcatch.com",
        "mail-temporaire.fr",
        "moakt.com",
        "tmpmail.org",
        "tmpmail.net",
        "inboxkitten.com",
        "emailfake.com",
        "fakemail.net",
        "linshiyouxiang.net",
        "spambox.us",
        "dropmail.me",
        "33mail.com",
        "anonaddy.me",
        "armyspy.com",
        "cuvox.de",
        "dayrep.com",
        "einrot.com",
        "fleckens.hu",
        "gustr.com",
        "jourrapide.com",
        "rhyta.com",
        "superrito.com",
        "teleworm.us",
    };

    /// <summary>Tells whether a domain, or the domain it is a subdomain of, is on the list.</summary>
    /// <param name="domain">The e-mail domain, in lower case.</param>
    /// <returns><see langword="true"/> for a disposable mailbox domain.</returns>
    public static bool Contains(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return All.Any(listed => EmailDomains.IsSameOrSubdomain(domain, listed));
    }
}
