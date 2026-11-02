namespace Innovayse.Hostpanel.Domain.Clients;

/// <summary>
/// Refusal codes the <see cref="Client"/> aggregate throws as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so <c>ResourceCoverageTests</c> (which only reflects over
/// <c>HostpanelErrorCodes</c>) still requires a sentence for each one in every language.
/// </remarks>
public static class ClientsErrors
{
    /// <summary>The id names no contact on this client (404): <see cref="Client.UpdateContact"/> and removal.</summary>
    public const string ClientContactNotFound = "CLIENT_CONTACT_NOT_FOUND";

    /// <summary>The account owner cannot also be added as an additional user (400): <see cref="Client.AddUser"/>.</summary>
    public const string ClientUserIsOwner = "CLIENT_USER_IS_OWNER";

    /// <summary>The user is already linked to this client (400): <see cref="Client.AddUser"/>.</summary>
    public const string ClientUserLinkDuplicate = "CLIENT_USER_LINK_DUPLICATE";

    /// <summary>
    /// The user is not linked to this client (400): <see cref="Client.RemoveUser"/> and
    /// <see cref="Client.UpdateUserPermissions"/>.
    /// </summary>
    public const string ClientUserNotLinked = "CLIENT_USER_NOT_LINKED";

    /// <summary>The named user is already the account owner (400): <see cref="Client.TransferOwnership"/>.</summary>
    public const string ClientTransferSameOwner = "CLIENT_TRANSFER_SAME_OWNER";

    /// <summary>
    /// The account owner cannot be removed as an additional user (409): <see cref="Client.RemoveUser"/>;
    /// the owner changes only through <see cref="Client.TransferOwnership"/>.
    /// </summary>
    public const string ClientOwnerCannotBeRemoved = "CLIENT_OWNER_CANNOT_BE_REMOVED";

    /// <summary>Another contact of this client already uses the email address (409): <see cref="Client.AddContact"/> and <see cref="Client.UpdateContact"/>.</summary>
    public const string ClientContactEmailInUse = "CONTACT_EMAIL_IN_USE";

    /// <summary>
    /// The account is closed, so its contacts and users can no longer be changed (409): every
    /// contact and user change on <see cref="Client"/>. Distinct from <see cref="ClientClosed"/>,
    /// which refuses orders and payments.
    /// </summary>
    public const string ClientRecordClosed = "CLIENT_RECORD_CLOSED";

    /// <summary>The deduction exceeds the client's credit balance (400): <see cref="Client.DeductCredit"/>.</summary>
    public const string ClientCreditInsufficient = "CLIENT_CREDIT_INSUFFICIENT";

    /// <summary>
    /// The signed-in user is not allowed to do this on the account: the permission the action needs
    /// was not granted, or the user has no relationship to any account (403). Raised by the client
    /// portal's permission filter, not by the aggregate; declared here with the other client codes.
    /// </summary>
    public const string ClientPermissionDenied = "CLIENT_PERMISSION_DENIED";

    /// <summary>
    /// The client holds a credit balance, so the billing currency cannot change (409): <see cref="Client.SetCurrency"/>.
    /// The balance has no currency of its own, so a change would silently re-value money the client prepaid.
    /// </summary>
    public const string ClientCurrencyLockedByCredit = "CLIENT_CURRENCY_LOCKED_BY_CREDIT";

    /// <summary>The account is not in a state that can be suspended (400): <see cref="Client.Suspend"/>.</summary>
    public const string ClientSuspendNotAllowed = "CLIENT_SUSPEND_NOT_ALLOWED";

    /// <summary>The account is already closed (400): <see cref="Client.Close"/>.</summary>
    public const string ClientCloseNotAllowed = "CLIENT_CLOSE_NOT_ALLOWED";

    /// <summary>A closed account cannot be reactivated this way (400): <see cref="Client.Activate"/>.</summary>
    public const string ClientActivateNotAllowed = "CLIENT_ACTIVATE_NOT_ALLOWED";

    /// <summary>The account is suspended or closed and cannot be deactivated (400): <see cref="Client.Deactivate"/>.</summary>
    public const string ClientDeactivateNotAllowed = "CLIENT_DEACTIVATE_NOT_ALLOWED";

    /// <summary>
    /// The account is suspended, so it may not place orders or pay through a new card method (403).
    /// Raised by the order and payment use cases, not by the aggregate; declared here with the other client codes.
    /// </summary>
    public const string ClientSuspended = "CLIENT_SUSPENDED";

    /// <summary>
    /// The account is closed, so it may not place orders or pay through a new card method (403).
    /// Raised by the order, payment and renewal use cases, not by the aggregate; declared here with the other client codes.
    /// </summary>
    public const string ClientClosed = "CLIENT_CLOSED";

    /// <summary>
    /// The account has credit card processing disabled, so no card payment or saved card charge is taken from it (403).
    /// Raised by the card payment use cases, not by the aggregate; declared here with the other client codes.
    /// </summary>
    public const string ClientCardPaymentDisabled = "CLIENT_CARD_PAYMENT_DISABLED";

    /// <summary>
    /// The account does not allow single sign-on into the hosting panels of its services (403).
    /// Raised by the client-facing panel sign-in use case, not by the aggregate; declared here with the other client codes.
    /// </summary>
    public const string ClientSsoDisabled = "CLIENT_SSO_DISABLED";

    /// <summary>
    /// The account is tax exempt, so a tax rate cannot be set on its invoices (409).
    /// Raised by the invoice options use case, not by the aggregate; declared here with the other client codes.
    /// </summary>
    public const string ClientTaxExempt = "CLIENT_TAX_EXEMPT";
}
