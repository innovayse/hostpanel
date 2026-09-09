namespace Innovayse.Infrastructure.Notifications.Options;

/// <summary>
/// The sender identity stamped into the <c>From</c> header of every notification this product
/// sends. Bound from the "Smtp:From" subsection, which a deployment fills as
/// <c>Smtp__From__Address</c> and <c>Smtp__From__Name</c>.
/// </summary>
/// <remarks>
/// A nested subsection rather than the flat <c>Smtp__FromEmail</c> / <c>Smtp__FromName</c> pair
/// this product used to read, because the nested shape is the one the rest of the Innovayse
/// platform's mail configuration already uses and hostpanel was the only product spelling it
/// differently. The two values are one thing -- an address and the name shown beside it -- and
/// neither is meaningful without the other.
/// </remarks>
public sealed class SmtpFromOptions
{
    /// <summary>
    /// Gets the sender email address. Carries no default: the one that used to be baked into
    /// <c>appsettings.json</c> was this operator's own, which is the wrong address for anyone
    /// else running this product and the wrong thing to discover on delivered mail.
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Gets the sender display name shown beside <see cref="Address"/> in a mail client. A
    /// human-readable brand name, not an address.
    /// </summary>
    public required string Name { get; init; }
}
