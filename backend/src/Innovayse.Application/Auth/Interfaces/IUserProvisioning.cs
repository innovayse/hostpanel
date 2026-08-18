namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Creates and edits the records that describe people.
///
/// <para>
/// Separate from <see cref="IIdentityProvider"/>, which only reads, because only one of
/// the two deployments may do this at all. Where this product owns its users it creates
/// and edits them; where an SSO owns them it must not, and every implementation of this
/// interface in that mode refuses.
/// </para>
///
/// <para>
/// Refusing rather than omitting the interface is deliberate. A flow that still tries to
/// create a person in SSO mode is a flow nobody has finished thinking about, and it should
/// say so where it happens — loudly, naming what to do instead — rather than fail to
/// resolve at start-up in a stack trace that names dependency injection.
/// </para>
/// </summary>
public interface IUserProvisioning
{
    /// <summary>
    /// Creates a person and returns their subject.
    /// </summary>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task<string> CreateAsync(
        string email, string? firstName, string? lastName, CancellationToken ct);

    /// <summary>
    /// Updates a person's name.
    /// </summary>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task UpdateNameAsync(string subject, string firstName, string lastName, CancellationToken ct);
}

/// <summary>
/// Thrown when a flow tries to create or edit a person in a deployment whose people belong
/// to an SSO.
/// </summary>
public sealed class UserProvisioningNotAllowedException(string operation)
    : InvalidOperationException(
        $"This deployment's accounts belong to the SSO, so hostpanel cannot {operation}. " +
        "The person must exist in the SSO first, and changes to their name or address are " +
        "made there.");
