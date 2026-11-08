using System.Diagnostics.CodeAnalysis;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Application.Common.Errors;

/// <summary>
/// Every error code a hostpanel handler can refuse with, declared once. A code is a resource key:
/// the sentence a person reads is <c>ErrorMessages.resx[code]</c>, resolved at the API boundary in
/// the request's language, and <c>ResourceCoverageTests</c> fails the build when a code has no
/// sentence in any language. Never a literal anywhere else.
/// </summary>
/// <remarks>
/// The status each code answers with comes from <c>StatusMapping</c> by its name, so a new code is
/// named for the status it must produce. Existing codes are never renamed: the admin and client
/// frontends branch on them.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "Constant names mirror the SCREAMING_SNAKE wire codes (rules/cqrs.md).")]
public static class HostpanelErrorCodes
{
    /// <summary>An unhandled fault (500); also the fallback sentence for a code with no entry.</summary>
    public const string HOSTPANEL_UNEXPECTED = SharedErrorCodes.Unexpected;

    /// <summary>The edge rate limiter refused the request (429, with <c>Retry-After</c>).</summary>
    public const string HOSTPANEL_RATE_LIMITED = SharedErrorCodes.RateLimited;

    /// <summary>
    /// A command marked <c>IDemoRestricted</c> was refused because the deployment is a public demo
    /// (403, an exact <c>StatusMapping</c> row). Thrown by the demo guard middleware, not by a handler.
    /// </summary>
    public const string DEMO_ACTION_DISABLED = SharedErrorCodes.DemoActionDisabled;

    // Auth

    /// <summary>
    /// First-run setup was attempted on an installation that already has an Admin (409). Replaces
    /// the thrown <c>SetupAlreadyCompletedException</c>; the code is unchanged.
    /// </summary>
    public const string SETUP_ALREADY_COMPLETED = "SETUP_ALREADY_COMPLETED";

    /// <summary>
    /// First-run setup was attempted without the token this installation issued, or with the wrong
    /// one (403, via the <c>_TOKEN_INVALID</c> suffix). Replaces the thrown
    /// <c>SetupTokenInvalidException</c>; the code is unchanged.
    /// </summary>
    public const string SETUP_TOKEN_INVALID = "SETUP_TOKEN_INVALID";

    /// <summary>The presented TOTP code did not match the account's secret (400).</summary>
    public const string TWO_FACTOR_CODE_INVALID = "TWO_FACTOR_CODE_INVALID";

    /// <summary>The email and password match no account, or a sign-in step could not resolve its account (401).</summary>
    public const string SIGN_IN_UNAUTHORIZED = "SIGN_IN_UNAUTHORIZED";

    /// <summary>The account is locked out after too many wrong passwords; no password is accepted until the lockout ends (423, via the <c>_LOCKED</c> suffix).</summary>
    public const string SIGN_IN_ACCOUNT_LOCKED = "SIGN_IN_ACCOUNT_LOCKED";

    /// <summary>The pending token handed out after the password step is malformed (401).</summary>
    public const string PENDING_TOKEN_UNAUTHORIZED = "PENDING_TOKEN_UNAUTHORIZED";

    /// <summary>The TOTP code presented at sign-in did not match (401). Distinct from <see cref="TWO_FACTOR_CODE_INVALID"/>, which answers 400 for a signed-in account.</summary>
    public const string TWO_FACTOR_CODE_UNAUTHORIZED = "TWO_FACTOR_CODE_UNAUTHORIZED";

    /// <summary>The sign-out fan-out presented a missing or wrong <c>X-Logout-Key</c> (401).</summary>
    public const string BACKCHANNEL_LOGOUT_UNAUTHORIZED = "BACKCHANNEL_LOGOUT_UNAUTHORIZED";

    /// <summary>A password-reset or email-confirmation token is wrong or past its lifetime (400).</summary>
    public const string TOKEN_INVALID_OR_EXPIRED = "TOKEN_INVALID_OR_EXPIRED";

    /// <summary>The local account could not be created, for example because the address is taken or the password is refused (400).</summary>
    public const string REGISTRATION_FAILED = "REGISTRATION_FAILED";

    /// <summary>An account with this email address already exists, so a new one cannot be created for it (409).</summary>
    public const string ACCOUNT_EMAIL_EXISTS = "ACCOUNT_EMAIL_EXISTS";

    /// <summary>Two-factor setup was started on an account that already has it on (409); switch it off first.</summary>
    public const string TWO_FACTOR_ALREADY_ENABLED = "TWO_FACTOR_ALREADY_ENABLED";

    /// <summary>The current password a sensitive account change asks for is missing or wrong (400).</summary>
    public const string CURRENT_PASSWORD_INVALID = "CURRENT_PASSWORD_INVALID";

    /// <summary>The new sign-in address already belongs to another account (409).</summary>
    public const string EMAIL_ALREADY_IN_USE = "EMAIL_ALREADY_IN_USE";

    // Billing: currencies

    /// <summary>The code names no configured currency (404).</summary>
    public const string CURRENCY_NOT_FOUND = "CURRENCY_NOT_FOUND";

    /// <summary>Product prices were asked to be recomputed for the base currency itself (400).</summary>
    public const string CURRENCY_BASE_NOT_RECOMPUTED = "CURRENCY_BASE_NOT_RECOMPUTED";

    /// <summary>No currency is marked as the base, so nothing can be converted (503).</summary>
    public const string BASE_CURRENCY_NOT_CONFIGURED = "BASE_CURRENCY_NOT_CONFIGURED";

    /// <summary>The exchange rate source did not answer or answered with an error; nothing was changed (503).</summary>
    public const string EXCHANGE_RATE_SOURCE_UNAVAILABLE = "EXCHANGE_RATE_SOURCE_UNAVAILABLE";

    /// <summary>The exchange rate source quotes nothing against the configured base currency (409).</summary>
    public const string EXCHANGE_RATE_BASE_NOT_QUOTED = "EXCHANGE_RATE_BASE_NOT_QUOTED";

    // Billing: invoices

    /// <summary>The id names no invoice (404).</summary>
    public const string INVOICE_NOT_FOUND = "INVOICE_NOT_FOUND";

    /// <summary>The invoice has more lines than <c>InvoicePdf:MaxLines</c> allows in a PDF (400).</summary>
    public const string INVOICE_PDF_EXCEEDS_LIMIT = "INVOICE_PDF_EXCEEDS_LIMIT";

    /// <summary>No invoice PDF render slot came free for the request within <c>InvoicePdf:MaxRenderWaitSeconds</c> (503, with <c>Retry-After</c>).</summary>
    public const string INVOICE_PDF_RENDER_BUSY = "INVOICE_PDF_RENDER_BUSY";

    // Billing: quotes

    /// <summary>The id names no quote (404).</summary>
    public const string QUOTE_NOT_FOUND = "QUOTE_NOT_FOUND";

    // Billing: billable items

    /// <summary>The id names no billable item (404).</summary>
    public const string BILLABLE_ITEM_NOT_FOUND = "BILLABLE_ITEM_NOT_FOUND";

    /// <summary>A named billable item belongs to a different client than the one billed (400).</summary>
    public const string BILLABLE_ITEM_CLIENT_MISMATCH = "BILLABLE_ITEM_CLIENT_MISMATCH";

    /// <summary>A named billable item has already been placed on an invoice (400).</summary>
    public const string BILLABLE_ITEM_INVOICED = "BILLABLE_ITEM_INVOICED";

    // Billing: transactions and invoice payments

    /// <summary>The id names no transaction (404).</summary>
    public const string TRANSACTION_NOT_FOUND = "TRANSACTION_NOT_FOUND";

    /// <summary>The id names no client (404).</summary>
    public const string CLIENT_NOT_FOUND = "CLIENT_NOT_FOUND";

    /// <summary>The requested bulk action name is not one this endpoint knows (400).</summary>
    public const string BULK_INVOICE_ACTION_UNKNOWN = "BULK_INVOICE_ACTION_UNKNOWN";

    /// <summary>
    /// The invoice was paid through a hosted-gateway module with no configured plugin, so the
    /// refund cannot be executed against it (503).
    /// </summary>
    public const string PAYMENT_PLUGIN_NOT_CONFIGURED = "PAYMENT_PLUGIN_NOT_CONFIGURED";

    // Billing: fulfilment

    /// <summary>The id names no fulfilment record on this invoice (404).</summary>
    public const string FULFILMENT_RECORD_NOT_FOUND = "FULFILMENT_RECORD_NOT_FOUND";

    /// <summary>
    /// The fulfilment record has not failed, so there is nothing to retry (409, an exact
    /// <c>StatusMapping</c> row): only a failed record may be sent again,
    /// <see cref="Domain.Billing.FulfilmentRecord.Retry"/>.
    /// </summary>
    public const string FULFILMENT_RECORD_NOT_RETRYABLE = Domain.Billing.BillingErrors.FulfilmentRecordNotRetryable;

    /// <summary>
    /// The line is not a domain renewal left claimed, so there is no claim to release (409, an exact
    /// <c>StatusMapping</c> row): <see cref="Domain.Billing.Invoice.ReleaseRenewalClaim"/>.
    /// </summary>
    public const string RENEWAL_CLAIM_NOT_HELD = Domain.Billing.BillingErrors.RenewalClaimNotHeld;

    /// <summary>
    /// A fulfilment plugin could not be asked how much of the payment may be refunded, so the refund
    /// is refused and no money moved (503, an exact <c>StatusMapping</c> row).
    /// </summary>
    public const string FULFILMENT_ADVICE_UNAVAILABLE = "FULFILMENT_ADVICE_UNAVAILABLE";

    // Billing: gateway payments and saved payment methods

    /// <summary>The invoice is not in a status a payment may be started or charged for (400).</summary>
    public const string INVOICE_NOT_PAYABLE = "INVOICE_NOT_PAYABLE";

    /// <summary>The invoice already has a gateway session inside the bank's live window (409).</summary>
    public const string GATEWAY_SESSION_ALREADY_STARTED = "GATEWAY_SESSION_ALREADY_STARTED";

    /// <summary>The invoice's billing currency does not match the resolved gateway plugin's (400).</summary>
    public const string GATEWAY_CURRENCY_MISMATCH = "GATEWAY_CURRENCY_MISMATCH";

    /// <summary>The invoice carries no hosted-gateway session to verify (404).</summary>
    public const string GATEWAY_SESSION_NOT_FOUND = "GATEWAY_SESSION_NOT_FOUND";

    /// <summary>The caller's account has no saved payment method yet (400).</summary>
    public const string NO_SAVED_PAYMENT_METHODS = "NO_SAVED_PAYMENT_METHODS";

    /// <summary>
    /// The gateway return URL is not an absolute URL, or its origin is not one this deployment
    /// is configured to hand a payer back to (400).
    /// </summary>
    public const string RETURN_URL_NOT_ALLOWED = "RETURN_URL_NOT_ALLOWED";

    /// <summary>
    /// The enabled identity plugin threw, timed out, or two identity plugins are enabled, so nobody can be told
    /// which client account they act for; the request is refused and may be retried (503, with <c>Retry-After</c>).
    /// </summary>
    public const string IDENTITY_PLUGIN_UNAVAILABLE = "IDENTITY_PLUGIN_UNAVAILABLE";

    /// <summary>
    /// The card gateway could not be reached or failed on its side, so nothing was charged or
    /// nothing is known yet; the same attempt may be retried (503).
    /// </summary>
    public const string PAYMENT_GATEWAY_SERVICE_UNAVAILABLE = "PAYMENT_GATEWAY_SERVICE_UNAVAILABLE";

    /// <summary>The caller has no row in the clients table, so there is no account to act on (404).</summary>
    public const string CLIENT_PROFILE_NOT_FOUND = "CLIENT_PROFILE_NOT_FOUND";

    // Clients

    /// <summary>The token names no invitation (404).</summary>
    public const string INVITATION_NOT_FOUND = "INVITATION_NOT_FOUND";

    /// <summary>The invitation's window to accept it has passed (400).</summary>
    public const string INVITATION_EXPIRED = "INVITATION_EXPIRED";

    /// <summary>The invitation has already been accepted (409).</summary>
    public const string INVITATION_ALREADY_ACCEPTED = "INVITATION_ALREADY_ACCEPTED";

    /// <summary>The invited address already has a pending invitation on this account (409).</summary>
    public const string INVITATION_ALREADY_PENDING = "INVITATION_ALREADY_PENDING";

    /// <summary>The id names no identity account (404).</summary>
    public const string IDENTITY_USER_NOT_FOUND = "IDENTITY_USER_NOT_FOUND";

    /// <summary>The id names no live user account (404).</summary>
    public const string USER_NOT_FOUND = "USER_NOT_FOUND";

    /// <summary>An admin asked to delete their own account (409).</summary>
    public const string USER_CANNOT_DELETE_SELF = "USER_CANNOT_DELETE_SELF";

    /// <summary>The change would leave the installation with no active administrator (409).</summary>
    public const string USER_LAST_ADMIN = "USER_LAST_ADMIN";

    /// <summary>The account's owner has no identity record to read (404).</summary>
    public const string ACCOUNT_OWNER_NOT_FOUND = "ACCOUNT_OWNER_NOT_FOUND";

    /// <summary>The address invited names the account's own owner (400).</summary>
    public const string CANNOT_INVITE_ACCOUNT_OWNER = "CANNOT_INVITE_ACCOUNT_OWNER";

    /// <summary>The invited address is already linked to this account (409).</summary>
    public const string USER_ALREADY_LINKED = "USER_ALREADY_LINKED";

    /// <summary>The existing user named already has a client account of their own (409).</summary>
    public const string EXISTING_USER_ALREADY_HAS_CLIENT = "EXISTING_USER_ALREADY_HAS_CLIENT";

    /// <summary>The client has already been invoiced, so its currency is fixed (409).</summary>
    public const string CLIENT_CURRENCY_ALREADY_INVOICED = "CLIENT_CURRENCY_ALREADY_INVOICED";

    /// <summary>The external account is already linked to another client, so it cannot be linked to this one (409).</summary>
    public const string EXTERNAL_ACCOUNT_ALREADY_LINKED = "EXTERNAL_ACCOUNT_ALREADY_LINKED";

    // Notifications

    /// <summary>
    /// The id names no email log entry sent to the caller's own account (404). Answered alike
    /// whether the entry does not exist or was sent to somebody else, so
    /// <c>/api/me/emails/{id}</c> cannot be used to enumerate ids.
    /// </summary>
    public const string EMAIL_LOG_NOT_FOUND = "EMAIL_LOG_NOT_FOUND";

    /// <summary>
    /// The id names no email template (404): <c>GetEmailTemplateHandler</c>,
    /// <c>UpdateEmailTemplateHandler</c>, <c>DeleteEmailTemplateHandler</c>,
    /// <c>ToggleEmailTemplateHandler</c> and <c>SendEmailHandler</c>'s slug lookup all answer with
    /// it. Nothing binds <c>SendEmailCommand</c> to a route, so the last one never reaches the
    /// wire, but the handler still answers with a code rather than throwing.
    /// </summary>
    public const string EMAIL_TEMPLATE_NOT_FOUND = "EMAIL_TEMPLATE_NOT_FOUND";

    /// <summary>
    /// A mail log row already recorded as delivered was asked to be claimed again. The send handler asks
    /// <c>EmailLog.MayResend</c> first, so this only surfaces when that check is skipped.
    /// </summary>
    public const string EMAIL_ALREADY_DELIVERED = Domain.Notifications.NotificationsErrors.EmailAlreadyDelivered;

    /// <summary>
    /// The template already has a version in the requested language (409):
    /// <c>CreateEmailTemplateHandler</c>; each slug holds one version per language.
    /// </summary>
    public const string EMAIL_TEMPLATE_ALREADY_EXISTS = "EMAIL_TEMPLATE_ALREADY_EXISTS";

    // Support: tickets

    /// <summary>
    /// The id names no ticket (404). Shared by the staff surface and by <c>ITicketOwnership</c>'s
    /// client-facing refusal, so the whole tickets surface refuses alike.
    /// </summary>
    public const string TICKET_NOT_FOUND = "TICKET_NOT_FOUND";

    // Support: departments

    /// <summary>The id names no support department (404).</summary>
    public const string DEPARTMENT_NOT_FOUND = "DEPARTMENT_NOT_FOUND";

    // Support: downloads

    /// <summary>The id names no download (404).</summary>
    public const string DOWNLOAD_NOT_FOUND = "DOWNLOAD_NOT_FOUND";

    /// <summary>The id names no download category (404).</summary>
    public const string DOWNLOAD_CATEGORY_NOT_FOUND = "DOWNLOAD_CATEGORY_NOT_FOUND";

    /// <summary>The download category still has sub-categories or downloads, so it cannot be deleted (409).</summary>
    public const string DOWNLOAD_CATEGORY_IN_USE = "DOWNLOAD_CATEGORY_IN_USE";

    // Support: predefined replies

    /// <summary>The id names no predefined reply (404).</summary>
    public const string PREDEFINED_REPLY_NOT_FOUND = "PREDEFINED_REPLY_NOT_FOUND";

    /// <summary>The id names no predefined reply category (404).</summary>
    public const string PREDEFINED_REPLY_CATEGORY_NOT_FOUND = "PREDEFINED_REPLY_CATEGORY_NOT_FOUND";

    /// <summary>The predefined reply category still has sub-categories or replies, so it cannot be deleted (409).</summary>
    public const string PREDEFINED_REPLY_CATEGORY_IN_USE = "PREDEFINED_REPLY_CATEGORY_IN_USE";

    // Support: contact form

    /// <summary>This deployment has no configured enquiry inbox for the contact form (503).</summary>
    public const string CONTACT_NOT_CONFIGURED = "CONTACT_NOT_CONFIGURED";

    /// <summary>The SMTP relay refused to send the contact form's message (502).</summary>
    public const string CONTACT_SEND_FAILED = "CONTACT_SEND_FAILED";

    // Domains

    /// <summary>
    /// The id names no domain (404). Shared by the admin surface and by
    /// <see cref="IDomainOwnership"/>'s client-facing refusal, so the whole domains surface
    /// refuses alike under one code.
    /// </summary>
    public const string DOMAIN_NOT_FOUND = "DOMAIN_NOT_FOUND";

    /// <summary>The name is already registered through this system (409).</summary>
    public const string DOMAIN_ALREADY_REGISTERED = "DOMAIN_ALREADY_REGISTERED";

    /// <summary>The registrar rejected the registration request (502).</summary>
    public const string DOMAIN_REGISTRATION_UPSTREAM_FAILED = "DOMAIN_REGISTRATION_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the renewal request (502).</summary>
    public const string DOMAIN_RENEWAL_UPSTREAM_FAILED = "DOMAIN_RENEWAL_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the incoming transfer request (502).</summary>
    public const string DOMAIN_TRANSFER_UPSTREAM_FAILED = "DOMAIN_TRANSFER_UPSTREAM_FAILED";

    /// <summary>The registrar rejected cancellation of a pending incoming transfer (502).</summary>
    public const string DOMAIN_TRANSFER_CANCEL_UPSTREAM_FAILED = "DOMAIN_TRANSFER_CANCEL_UPSTREAM_FAILED";

    /// <summary>
    /// The domain carries no registrar reference, so an outgoing transfer or a renewal cannot be
    /// placed against it (400).
    /// </summary>
    public const string DOMAIN_NO_REGISTRAR_REF = "DOMAIN_NO_REGISTRAR_REF";

    /// <summary>The registrar rejected the outgoing transfer initiation request (502).</summary>
    public const string DOMAIN_OUTGOING_TRANSFER_UPSTREAM_FAILED = "DOMAIN_OUTGOING_TRANSFER_UPSTREAM_FAILED";

    /// <summary>The registrar accepted the outgoing transfer but returned no EPP code (502).</summary>
    public const string DOMAIN_EPP_CODE_UPSTREAM_FAILED = "DOMAIN_EPP_CODE_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the auto-renew update request (502).</summary>
    public const string DOMAIN_AUTO_RENEW_UPSTREAM_FAILED = "DOMAIN_AUTO_RENEW_UPSTREAM_FAILED";

    /// <summary>The registrar returned no WHOIS record for the name (404).</summary>
    public const string DOMAIN_WHOIS_NOT_FOUND = "DOMAIN_WHOIS_NOT_FOUND";

    /// <summary>
    /// This installation has no active product of type <c>Domain</c> configured, so a domain
    /// renewal or transfer-in cannot be sold as an order (503). A deployment gap, not a caller
    /// error.
    /// </summary>
    public const string DOMAIN_PRODUCT_NOT_CONFIGURED = "DOMAIN_PRODUCT_NOT_CONFIGURED";

    /// <summary>
    /// A client renew order was refused because a renewal invoice for the domain cannot be released:
    /// it is overdue and bills other items too (no longer editable), or it is PaymentPending or in
    /// Collections (a payment may be in flight); the client settles that invoice first (409).
    /// </summary>
    public const string RENEWAL_INVOICE_OPEN = "RENEWAL_INVOICE_OPEN";

    // Domains: DNS and forwarding

    /// <summary>The id names no DNS record on the domain's zone (404).</summary>
    public const string DNS_RECORD_NOT_FOUND = "DNS_RECORD_NOT_FOUND";

    /// <summary>The registrar rejected adding, updating or deleting a DNS record (502).</summary>
    public const string DNS_RECORD_UPSTREAM_FAILED = "DNS_RECORD_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the nameserver update request (502).</summary>
    public const string NAMESERVERS_UPSTREAM_FAILED = "NAMESERVERS_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the DNS management toggle request (502).</summary>
    public const string DNS_MANAGEMENT_UPSTREAM_FAILED = "DNS_MANAGEMENT_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the transfer-lock update request (502).</summary>
    public const string REGISTRAR_LOCK_UPSTREAM_FAILED = "REGISTRAR_LOCK_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the WHOIS privacy update request (502).</summary>
    public const string WHOIS_PRIVACY_UPSTREAM_FAILED = "WHOIS_PRIVACY_UPSTREAM_FAILED";

    /// <summary>The registrar holds no registrant contact record for the name (404).</summary>
    public const string DOMAIN_CONTACT_NOT_FOUND = "DOMAIN_CONTACT_NOT_FOUND";

    /// <summary>The registrar rejected the registrant contact update request (502).</summary>
    public const string DOMAIN_CONTACT_UPSTREAM_FAILED = "DOMAIN_CONTACT_UPSTREAM_FAILED";

    /// <summary>The registrar rejected the email forwarding toggle request (502).</summary>
    public const string EMAIL_FORWARDING_UPSTREAM_FAILED = "EMAIL_FORWARDING_UPSTREAM_FAILED";

    /// <summary>The id names no email forwarding rule on the domain (404).</summary>
    public const string EMAIL_FORWARDING_RULE_NOT_FOUND = "EMAIL_FORWARDING_RULE_NOT_FOUND";

    /// <summary>
    /// The registrar rejected adding, updating or deleting an email forwarding rule (502).
    /// </summary>
    public const string EMAIL_FORWARDING_RULE_UPSTREAM_FAILED = "EMAIL_FORWARDING_RULE_UPSTREAM_FAILED";

    /// <summary>The id names no TLD configuration (404).</summary>
    public const string TLD_CONFIG_NOT_FOUND = "TLD_CONFIG_NOT_FOUND";

    /// <summary>A TLD configuration for the extension already exists (409).</summary>
    public const string TLD_CONFIG_ALREADY_EXISTS = "TLD_CONFIG_ALREADY_EXISTS";

    /// <summary>The registrar module named does not match a known provider (400).</summary>
    public const string TLD_REGISTRAR_MODULE_UNKNOWN = "TLD_REGISTRAR_MODULE_UNKNOWN";

    // Domains: why a registrar adapter refused. The adapter logs the registrar's own wording and
    // hands one of these back on RegistrarResult.ErrorCode.

    /// <summary>The domain's registrar has no credentials configured (503).</summary>
    public const string REGISTRAR_NOT_CONFIGURED = "REGISTRAR_NOT_CONFIGURED";

    /// <summary>The registrar refused the order or change, or did not confirm it (502).</summary>
    public const string REGISTRAR_ORDER_UPSTREAM_FAILED = "REGISTRAR_ORDER_UPSTREAM_FAILED";

    /// <summary>The registrar offers no programmatic way to do this; its support has to (409).</summary>
    public const string REGISTRAR_OPERATION_UNSUPPORTED = "REGISTRAR_OPERATION_UNSUPPORTED";

    /// <summary>The registrar account holds no domain by that name (404).</summary>
    public const string REGISTRAR_DOMAIN_NOT_FOUND = "REGISTRAR_DOMAIN_NOT_FOUND";

    /// <summary>The registrar holds the domain but does not report it active yet (409).</summary>
    public const string REGISTRAR_DOMAIN_NOT_ACTIVE = "REGISTRAR_DOMAIN_NOT_ACTIVE";

    /// <summary>The domain has no privacy subscription at the registrar to switch on or off (409).</summary>
    public const string REGISTRAR_WHOIS_PRIVACY_UNAVAILABLE = "REGISTRAR_WHOIS_PRIVACY_UNAVAILABLE";

    /// <summary>The registrar says the name is not available to register or transfer, and the account does not hold it (409).</summary>
    public const string REGISTRAR_DOMAIN_UNAVAILABLE = "REGISTRAR_DOMAIN_UNAVAILABLE";

    /// <summary>The operator's registrar account has too little balance for the purchase (503).</summary>
    public const string REGISTRAR_INSUFFICIENT_FUNDS = "REGISTRAR_INSUFFICIENT_FUNDS";

    /// <summary>The registrant contact is missing a detail the registrar requires, so nothing was bought (422).</summary>
    public const string REGISTRAR_CONTACT_VALIDATION_FAILED = "REGISTRAR_CONTACT_VALIDATION_FAILED";

    // Support: knowledge base and content

    /// <summary>The id names no knowledge base article (404).</summary>
    public const string KBARTICLE_NOT_FOUND = "KBARTICLE_NOT_FOUND";

    /// <summary>The id names no knowledge base category (404).</summary>
    public const string KBCATEGORY_NOT_FOUND = "KBCATEGORY_NOT_FOUND";

    /// <summary>The knowledge base category still has sub-categories, so it cannot be deleted (409).</summary>
    public const string KBCATEGORY_IN_USE = "KBCATEGORY_IN_USE";

    /// <summary>The id names no announcement (404).</summary>
    public const string ANNOUNCEMENT_NOT_FOUND = "ANNOUNCEMENT_NOT_FOUND";

    /// <summary>The id names no network issue (404).</summary>
    public const string NETWORKISSUE_NOT_FOUND = "NETWORKISSUE_NOT_FOUND";

    // Orders

    /// <summary>
    /// The id names no order, or, for the four checkout-payment actions a guest reaches with no
    /// credential, the id and the payment token presented for it do not go together (404). Both
    /// causes answer alike on purpose: order ids are sequential, and a caller who could tell
    /// "no such order" apart from "not your order" could map out how many orders exist.
    /// </summary>
    public const string ORDER_NOT_FOUND = "ORDER_NOT_FOUND";

    /// <summary>
    /// The order is not Pending or Cancelled, so it may not be permanently deleted (400).
    /// </summary>
    public const string ORDER_NOT_DELETABLE = "ORDER_NOT_DELETABLE";

    /// <summary>
    /// The order carries no linked invoice yet, so there is nothing to pay or fulfil (400). Every
    /// order is linked to its invoice in the same unit of work that creates it, so this is a
    /// defensive answer for a state <c>PlaceOrder</c> never actually leaves behind.
    /// </summary>
    public const string ORDER_NOT_INVOICED = "ORDER_NOT_INVOICED";

    /// <summary>
    /// The checkout named a payment method that is not configured and switched on right now
    /// (400). The list a checkout shows and the methods this refusal checks against come from the
    /// same query, so what is shown and what is accepted never disagree.
    /// </summary>
    public const string ORDER_PAYMENT_METHOD_UNAVAILABLE = "ORDER_PAYMENT_METHOD_UNAVAILABLE";

    /// <summary>
    /// The checkout chose a currency this deployment does not offer, or has switched off (400).
    /// </summary>
    public const string ORDER_CURRENCY_UNAVAILABLE = "ORDER_CURRENCY_UNAVAILABLE";

    /// <summary>
    /// A guest checkout (no signed-in credential) is missing a field its registration needs
    /// (400). The validator already requires these of an anonymous caller, so this is the
    /// handler's own defensive answer for a command that reached it some other way.
    /// </summary>
    public const string ORDER_GUEST_DETAILS_REQUIRED = "ORDER_GUEST_DETAILS_REQUIRED";

    /// <summary>
    /// The ordered product has no price for the requested billing cycle in the payer's currency
    /// (400). The order refuses rather than converting from another currency, because a converted
    /// figure is not a price the operator set.
    /// </summary>
    public const string ORDER_PRODUCT_PRICE_UNAVAILABLE = "ORDER_PRODUCT_PRICE_UNAVAILABLE";

    /// <summary>
    /// A domain line in the order could not be priced (400): the name has no TLD, the TLD is not
    /// one this deployment sells, it is sold in a currency other than the payer's, the requested
    /// action is not one of register/transfer/renew, or the TLD has no published price for the
    /// requested period. The five causes are logged, never returned, so probing the checkout does
    /// not map out which TLDs this deployment carries.
    /// </summary>
    public const string ORDER_DOMAIN_PRICING_INVALID = "ORDER_DOMAIN_PRICING_INVALID";

    /// <summary>
    /// The order number sequence has outgrown the configured prefix and the longest order number
    /// the order table stores, so no new order can be numbered until the operator shortens the
    /// prefix (503).
    /// </summary>
    public const string ORDER_NUMBER_RANGE_EXHAUSTED = "ORDER_NUMBER_RANGE_EXHAUSTED";

    // Services

    /// <summary>
    /// The id names no client service (404). Shared by the admin surface and by every
    /// client-facing <c>My*</c> refusal through <c>IServiceOwnership</c>: a service belonging to somebody else, one
    /// that does not exist, and a caller with no client record all answer this code alike.
    /// </summary>
    public const string SERVICE_NOT_FOUND = "SERVICE_NOT_FOUND";

    /// <summary>A service that is not terminated already holds that username on that server (409): <see cref="Services.Commands.AddExistingService.AddExistingServiceHandler"/>.</summary>
    public const string SERVICE_HOSTING_ACCOUNT_EXISTS = "SERVICE_HOSTING_ACCOUNT_EXISTS";

    // Services: SSH key management

    /// <summary>The pasted text is not a single, valid ED25519 public key (400).</summary>
    public const string SSH_KEY_INVALID = "SSH_KEY_INVALID";

    /// <summary>The pasted text is a private key; it is never stored or echoed (400).</summary>
    public const string SSH_PRIVATE_KEY_REJECTED = "SSH_PRIVATE_KEY_REJECTED";

    /// <summary>The key is already on the service, recorded here or present on the server (409).</summary>
    public const string SSH_KEY_ALREADY_INSTALLED = "SSH_KEY_ALREADY_INSTALLED";

    /// <summary>The service already holds the maximum number of SSH keys (409).</summary>
    public const string SSH_KEY_LIMIT_REACHED = "SSH_KEY_LIMIT_REACHED";

    /// <summary>The key id names no live key of this service; a key of another service answers the same (404).</summary>
    public const string SSH_KEY_NOT_FOUND = "SSH_KEY_NOT_FOUND";

    /// <summary>The key was issued on the service's previous server or account; nothing is sent anywhere for it (409).</summary>
    public const string SSH_KEY_STALE = "SSH_KEY_STALE";

    /// <summary>The service's state (inactive, unsupported panel, SSH not enabled on its server, shell disabled) does not allow key changes (409).</summary>
    public const string SSH_ACCESS_UNAVAILABLE = "SSH_ACCESS_UNAVAILABLE";

    /// <summary>The server's key agent failed or did not answer; the outcome is recorded on the key (503).</summary>
    public const string SSH_SERVER_UNAVAILABLE = "SSH_SERVER_UNAVAILABLE";

    /// <summary>
    /// The agent endpoint presented no host key to an administrator's scan: wrong host or port, a
    /// firewall, or sshd refusing the connection (503). Admin-facing, so it names what to check.
    /// </summary>
    public const string SSH_HOST_KEY_UNREADABLE = "SSH_HOST_KEY_UNREADABLE";

    /// <summary>The agent endpoint gave no answer within the timeout: no network path to it (503).</summary>
    public const string SSH_AGENT_HOST_UNREACHABLE = "SSH_AGENT_HOST_UNREACHABLE";

    /// <summary>The agent endpoint refused the connection: nothing listens there, or a firewall rejects it (503).</summary>
    public const string SSH_AGENT_PORT_REFUSED = "SSH_AGENT_PORT_REFUSED";

    /// <summary>The agent endpoint accepted the connection and closed it before the SSH greeting (503).</summary>
    public const string SSH_AGENT_CONNECTION_CLOSED = "SSH_AGENT_CONNECTION_CLOSED";

    /// <summary>The server's control panel module has no SSH key implementation (400).</summary>
    public const string SSH_MODULE_NOT_SUPPORTED = "SSH_MODULE_NOT_SUPPORTED";

    /// <summary>The server's SSH settings lack a value an enabled configuration or an agent call needs (400).</summary>
    public const string SSH_SETTINGS_INCOMPLETE = "SSH_SETTINGS_INCOMPLETE";

    /// <summary>SSH key management can only be enabled after a successful agent test with the current settings (409).</summary>
    public const string SSH_AGENT_NOT_VERIFIED = "SSH_AGENT_NOT_VERIFIED";

    /// <summary>Another server already uses this agent host and port for SSH key management (409).</summary>
    public const string SSH_AGENT_ENDPOINT_IN_USE = "SSH_AGENT_ENDPOINT_IN_USE";

    /// <summary>An open cancellation request already exists for the service (409).</summary>
    public const string CANCELLATION_ALREADY_PENDING = "CANCELLATION_ALREADY_PENDING";

    /// <summary>The service is already terminated, so there is nothing left to cancel (409).</summary>
    public const string SERVICE_ALREADY_TERMINATED = "SERVICE_ALREADY_TERMINATED";

    /// <summary>The id names no cancellation request (404).</summary>
    public const string CANCELLATION_REQUEST_NOT_FOUND = "CANCELLATION_REQUEST_NOT_FOUND";

    /// <summary>The id names no product (404).</summary>
    public const string PRODUCT_NOT_FOUND = "PRODUCT_NOT_FOUND";

    /// <summary>
    /// The product is not active, or an amount was left for the handler to price and the product
    /// has no price for that cycle in the ordering client's currency (400).
    /// </summary>
    public const string PRODUCT_NOT_AVAILABLE = "PRODUCT_NOT_AVAILABLE";

    /// <summary>The service is not Pending, so it cannot be set up (400).</summary>
    public const string SERVICE_NOT_PENDING = "SERVICE_NOT_PENDING";

    /// <summary>
    /// No server of the product's module has room to provision the service right now (503): a
    /// capacity gap, not a caller error.
    /// </summary>
    public const string NO_ELIGIBLE_SERVER = "NO_ELIGIBLE_SERVER";

    /// <summary>The provisioning provider rejected the setup request (502).</summary>
    public const string PROVISIONING_UPSTREAM_FAILED = "PROVISIONING_UPSTREAM_FAILED";

    /// <summary>
    /// The provisioning provider could not be reached, or its answer was lost, so nobody knows
    /// whether the account was created (503). Unlike a refusal this is a retry signal: the service
    /// stays Pending and a later attempt adopts the account when it exists.
    /// </summary>
    public const string PROVISIONING_OUTCOME_UNKNOWN = "PROVISIONING_OUTCOME_UNKNOWN";

    /// <summary>
    /// The service's first invoice is not paid in full, so the client may not set it up yet (402).
    /// Staff provisioning is not bound by it.
    /// </summary>
    public const string SERVICE_FIRST_INVOICE_UNPAID = "SERVICE_FIRST_INVOICE_UNPAID";

    /// <summary>
    /// The panel already holds an account under the service's username that is not this service's,
    /// so it cannot be adopted and the service needs staff attention (409).
    /// </summary>
    public const string PROVISIONING_ACCOUNT_CONFLICT = "PROVISIONING_ACCOUNT_CONFLICT";

    /// <summary>The service has no domain or username to create the hosting account with (409).</summary>
    public const string SERVICE_HOSTING_DETAILS_MISSING = "SERVICE_HOSTING_DETAILS_MISSING";

    /// <summary>The service's product names no hosting package to create the account on (409).</summary>
    public const string PRODUCT_PACKAGE_MISSING = "PRODUCT_PACKAGE_MISSING";

    /// <summary>
    /// The product has no server group and the servers run more than one supported panel, so which
    /// panel its package belongs to is ambiguous (409).
    /// </summary>
    public const string PRODUCT_SERVER_GROUP_REQUIRED = "PRODUCT_SERVER_GROUP_REQUIRED";

    // Provisioning

    /// <summary>
    /// The service exists but has never been given a provisioning reference, so there is no
    /// hosting account on any provider to act on (400).
    /// </summary>
    public const string SERVICE_NOT_PROVISIONED = "SERVICE_NOT_PROVISIONED";

    /// <summary>
    /// The service has a provisioning reference but no assigned server, or the server it names no
    /// longer exists, so no provider can be resolved for it (400).
    /// </summary>
    public const string SERVICE_NO_SERVER_ASSIGNED = "SERVICE_NO_SERVER_ASSIGNED";

    /// <summary>The provisioning provider rejected the password change (502).</summary>
    public const string SERVICE_PASSWORD_UPSTREAM_FAILED = "SERVICE_PASSWORD_UPSTREAM_FAILED";

    /// <summary>The provisioning provider rejected the package change (502).</summary>
    public const string SERVICE_PACKAGE_UPSTREAM_FAILED = "SERVICE_PACKAGE_UPSTREAM_FAILED";

    /// <summary>The provisioning provider rejected the suspension (502).</summary>
    public const string SERVICE_SUSPEND_UPSTREAM_FAILED = "SERVICE_SUSPEND_UPSTREAM_FAILED";

    /// <summary>The provisioning provider rejected the termination (502).</summary>
    public const string SERVICE_TERMINATE_UPSTREAM_FAILED = "SERVICE_TERMINATE_UPSTREAM_FAILED";

    /// <summary>The provisioning provider rejected the reactivation (502).</summary>
    public const string SERVICE_UNSUSPEND_UPSTREAM_FAILED = "SERVICE_UNSUSPEND_UPSTREAM_FAILED";

    /// <summary>The provisioning provider could not issue a control-panel single-sign-on URL (502).</summary>
    public const string SERVICE_CPANEL_SSO_UPSTREAM_FAILED = "SERVICE_CPANEL_SSO_UPSTREAM_FAILED";

    /// <summary>
    /// The control panel answered a single-sign-on address that is not an https address on its own
    /// server, so it was not passed on (502).
    /// </summary>
    public const string SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED = "SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED";

    /// <summary>The service's username is not one cPanel accepts, so the panel was not called (409).</summary>
    public const string CPANEL_USERNAME_INVALID = "CPANEL_USERNAME_INVALID";

    /// <summary>The service's domain is not a domain name cPanel accepts, so the panel was not called (409).</summary>
    public const string CPANEL_DOMAIN_INVALID = "CPANEL_DOMAIN_INVALID";

    /// <summary>The provisioning provider could not return the account's credentials (502).</summary>
    public const string SERVICE_CREDENTIALS_UPSTREAM_FAILED = "SERVICE_CREDENTIALS_UPSTREAM_FAILED";

    // Admin: servers

    /// <summary>The id names no configured provisioning server (404).</summary>
    public const string SERVER_NOT_FOUND = "SERVER_NOT_FOUND";

    /// <summary>The id names no configured server group (404).</summary>
    public const string SERVER_GROUP_NOT_FOUND = "SERVER_GROUP_NOT_FOUND";

    /// <summary>The server still hosts client services or SSH keys, so it cannot be deleted (409).</summary>
    public const string SERVER_IN_USE = "SERVER_IN_USE";

    /// <summary>Products still provision into the server group, so it cannot be deleted (409).</summary>
    public const string SERVER_GROUP_IN_USE = "SERVER_GROUP_IN_USE";

    /// <summary>The connection probe could not reach the server or the panel did not answer (answered inside the test result, not as a refusal).</summary>
    public const string SERVER_CONNECTION_UNREACHABLE = "SERVER_CONNECTION_UNREACHABLE";

    /// <summary>The server stores no access hash to probe a version 7 hosting panel with (answered inside the test result).</summary>
    public const string SERVER_CONNECTION_NO_ACCESS_HASH = "SERVER_CONNECTION_NO_ACCESS_HASH";

    /// <summary>The server stores no API key or access hash to probe a legacy hosting panel with (answered inside the test result).</summary>
    public const string SERVER_CONNECTION_NO_API_KEY = "SERVER_CONNECTION_NO_API_KEY";

    /// <summary>The server's module has no connection probe (answered inside the test result).</summary>
    public const string SERVER_CONNECTION_MODULE_UNSUPPORTED = "SERVER_CONNECTION_MODULE_UNSUPPORTED";

    // Admin: plugins

    /// <summary>
    /// The upload is empty or not a readable ZIP archive, it has no <c>plugin.json</c> at its root,
    /// the manifest could not be parsed, or it is missing the plugin id (400).
    /// </summary>
    public const string PLUGIN_MANIFEST_INVALID = "PLUGIN_MANIFEST_INVALID";

    /// <summary>The plugin was built against an SDK version this deployment does not support (400).</summary>
    public const string PLUGIN_SDK_VERSION_UNSUPPORTED = "PLUGIN_SDK_VERSION_UNSUPPORTED";

    /// <summary>The manifest declares a <c>manifestVersion</c> this host does not know (400).</summary>
    public const string PLUGIN_MANIFEST_VERSION_UNKNOWN = "PLUGIN_MANIFEST_VERSION_UNKNOWN";

    /// <summary>The plugin was built against an older SDK major version than the host's and has to be rebuilt (400).</summary>
    public const string PLUGIN_SDK_ASSEMBLY_OUTDATED = "PLUGIN_SDK_ASSEMBLY_OUTDATED";

    /// <summary>A route, page or slot entry names a permission that is neither a client permission nor <c>Any</c> (400).</summary>
    public const string PLUGIN_PERMISSION_UNKNOWN = "PLUGIN_PERMISSION_UNKNOWN";

    /// <summary>An admin route, page or slot entry declares a permission, which the admin area does not take (400).</summary>
    public const string PLUGIN_ADMIN_PERMISSION_DECLARED = "PLUGIN_ADMIN_PERMISSION_DECLARED";

    /// <summary>A route path is not made of lowercase segments and parameters, or has an empty segment or a dot segment (400).</summary>
    public const string PLUGIN_ROUTE_PATH_INVALID = "PLUGIN_ROUTE_PATH_INVALID";

    /// <summary>The same method and path template is declared twice in one area (400).</summary>
    public const string PLUGIN_ROUTE_DUPLICATE = "PLUGIN_ROUTE_DUPLICATE";

    /// <summary>A text key used by a menu, page, setting or slot is missing from the plugin's English texts (400).</summary>
    public const string PLUGIN_LABEL_KEY_MISSING = "PLUGIN_LABEL_KEY_MISSING";

    /// <summary>A slot entry names a slot the host does not offer (400).</summary>
    public const string PLUGIN_SLOT_UNKNOWN = "PLUGIN_SLOT_UNKNOWN";

    /// <summary>A slot entry asks for a context key its slot does not offer (400).</summary>
    public const string PLUGIN_SLOT_CONTEXT_KEY_NOT_OFFERED = "PLUGIN_SLOT_CONTEXT_KEY_NOT_OFFERED";

    /// <summary>A file in the uploaded plugin could not be read, so the plugin cannot be checked (400).</summary>
    public const string PLUGIN_ASSEMBLY_UNREADABLE = "PLUGIN_ASSEMBLY_UNREADABLE";

    /// <summary>A slot widget id is not lowercase letters, digits, hyphen and underscore (400).</summary>
    public const string PLUGIN_SLOT_WIDGET_ID_INVALID = "PLUGIN_SLOT_WIDGET_ID_INVALID";

    /// <summary>The same widget id is declared twice for one slot in one plugin (400).</summary>
    public const string PLUGIN_SLOT_WIDGET_DUPLICATE = "PLUGIN_SLOT_WIDGET_DUPLICATE";

    /// <summary>The manifest id is not a valid plugin id (400).</summary>
    public const string PLUGIN_MANIFEST_ID_INVALID = "PLUGIN_MANIFEST_ID_INVALID";

    /// <summary>The manifest version is not a strict semantic version such as 1.2.3 (400).</summary>
    public const string PLUGIN_MANIFEST_VERSION_INVALID = "PLUGIN_MANIFEST_VERSION_INVALID";

    /// <summary>A page or slot names a view document the plugin package does not hold, or a path that is not a package-relative .json file (400).</summary>
    public const string PLUGIN_VIEW_MISSING = "PLUGIN_VIEW_MISSING";

    /// <summary>A view document is not a JSON object with a schema and a blocks array, or a route reference in it is not a method and a path (400).</summary>
    public const string PLUGIN_VIEW_INVALID = "PLUGIN_VIEW_INVALID";

    /// <summary>A view document declares a schema version this host does not render (400).</summary>
    public const string PLUGIN_VIEW_SCHEMA_UNSUPPORTED = "PLUGIN_VIEW_SCHEMA_UNSUPPORTED";

    /// <summary>A view document holds a block whose kind the view schema does not define (400).</summary>
    public const string PLUGIN_VIEW_BLOCK_KIND_UNKNOWN = "PLUGIN_VIEW_BLOCK_KIND_UNKNOWN";

    /// <summary>A view document names a route that the plugin does not declare in the panel that shows the view (400).</summary>
    public const string PLUGIN_VIEW_ROUTE_UNDECLARED = "PLUGIN_VIEW_ROUTE_UNDECLARED";

    /// <summary>A plugin menu names an icon that is not 1 to 64 lowercase letters, digits and hyphens (400).</summary>
    public const string PLUGIN_MENU_ICON_INVALID = "PLUGIN_MENU_ICON_INVALID";

    /// <summary>The plugin manifest names an <c>icon</c> file the package does not hold (400).</summary>
    public const string PLUGIN_ICON_MISSING = "PLUGIN_ICON_MISSING";

    /// <summary>
    /// The plugin manifest <c>icon</c> is not a package-relative SVG or PNG file, is too large, is not a real PNG, or is
    /// an SVG that carries script, event handlers or external references (400).
    /// </summary>
    public const string PLUGIN_ICON_INVALID = "PLUGIN_ICON_INVALID";

    /// <summary>The plugin manifest declares the <c>endpoints</c> capability but the plugin class does not implement the endpoints contract (400).</summary>
    public const string PLUGIN_ENDPOINTS_NOT_IMPLEMENTED = "PLUGIN_ENDPOINTS_NOT_IMPLEMENTED";

    /// <summary>The plugin has no icon to serve: it is unknown, refused, or declares none (404).</summary>
    public const string PLUGIN_ICON_NOT_FOUND = "PLUGIN_ICON_NOT_FOUND";

    /// <summary>
    /// A plugin page id is not 1 to 64 lowercase letters, digits, hyphens and underscores starting with a letter or
    /// digit, or is declared twice in one panel (400); such a page could never be served.
    /// </summary>
    public const string PLUGIN_PAGE_ID_INVALID = "PLUGIN_PAGE_ID_INVALID";

    /// <summary>A plugin declares a callback route that carries a permission or streams, which a public callback cannot (400).</summary>
    public const string PLUGIN_CALLBACK_INVALID = "PLUGIN_CALLBACK_INVALID";

    /// <summary>The plugin is not available to this panel or declares no such page there (404); the two answer alike.</summary>
    public const string PLUGIN_PAGE_NOT_FOUND = "PLUGIN_PAGE_NOT_FOUND";

    /// <summary>A plugin that is not an app plugin declares api routes or ui menus, pages or slots without the endpoints capability (400).</summary>
    public const string PLUGIN_ENDPOINTS_NOT_DECLARED = "PLUGIN_ENDPOINTS_NOT_DECLARED";

    /// <summary>The manifest names a capability the host does not define, or one the plugin type may not carry (400).</summary>
    public const string PLUGIN_CAPABILITY_NOT_ALLOWED = "PLUGIN_CAPABILITY_NOT_ALLOWED";

    /// <summary>
    /// The manifest's <c>hostAccess</c> names a kind the host's SDK does not define, names one twice, or is declared in
    /// a version 1 manifest (400, the default arm: it is answered only by a plugin install).
    /// </summary>
    public const string PLUGIN_HOST_ACCESS_UNKNOWN = "PLUGIN_HOST_ACCESS_UNKNOWN";

    /// <summary>
    /// A view document holds a visibility or follow-up condition that is not a GET route, a dotted field and one value
    /// or a bounded list of values (400, the default arm: it is answered only by a plugin install).
    /// </summary>
    public const string PLUGIN_VIEW_CONDITION_INVALID = "PLUGIN_VIEW_CONDITION_INVALID";

    /// <summary>
    /// A view document holds a follow-up refresh that is malformed or names a block the document does not hold, or a
    /// form reset flag that is not a boolean (400, the default arm: it is answered only by a plugin install).
    /// </summary>
    public const string PLUGIN_VIEW_FOLLOW_UP_INVALID = "PLUGIN_VIEW_FOLLOW_UP_INVALID";

    /// <summary>
    /// A view document holds an action body built from page data in a shape or place the host does not accept (400, the
    /// default arm: it is answered only by a plugin install).
    /// </summary>
    public const string PLUGIN_VIEW_ACTION_BODY_INVALID = "PLUGIN_VIEW_ACTION_BODY_INVALID";

    /// <summary>A view document holds a preview or chat <c>selection</c> this server does not accept.</summary>
    public const string PLUGIN_VIEW_SELECTION_INVALID = "PLUGIN_VIEW_SELECTION_INVALID";

    /// <summary>
    /// A page declares a layout the host does not define, or a layout on an admin page (400, the default arm: it is
    /// answered only by a plugin install).
    /// </summary>
    public const string PLUGIN_PAGE_LAYOUT_UNKNOWN = "PLUGIN_PAGE_LAYOUT_UNKNOWN";

    /// <summary>
    /// A view document holds a split that is not exactly two panes of blocks, or one inside another (400, the default
    /// arm: it is answered only by a plugin install).
    /// </summary>
    public const string PLUGIN_VIEW_SPLIT_INVALID = "PLUGIN_VIEW_SPLIT_INVALID";

    /// <summary>
    /// A view document holds a preview with bad sources or size, or outside a client page (400, the default arm: it is
    /// answered only by a plugin install).
    /// </summary>
    public const string PLUGIN_VIEW_PREVIEW_INVALID = "PLUGIN_VIEW_PREVIEW_INVALID";

    /// <summary>
    /// A view document gives one block id to more than one block (400, the default arm: it is answered only by a plugin
    /// install).
    /// </summary>
    public const string PLUGIN_VIEW_BLOCK_ID_REPEATED = "PLUGIN_VIEW_BLOCK_ID_REPEATED";

    /// <summary>
    /// One of the archive's entries would write outside the plugin's own directory (400). The
    /// zip-slip guard's refusal, named for what a caller can act on rather than the attack it
    /// blocks.
    /// </summary>
    public const string PLUGIN_ARCHIVE_UNSAFE = "PLUGIN_ARCHIVE_UNSAFE";

    /// <summary>
    /// One of the archive's entries could not be written into the unpack folder, because a file and a folder of the
    /// archive share a name or the path is not writable (400). The package is at fault, not the server.
    /// </summary>
    public const string PLUGIN_ARCHIVE_UNWRITABLE = "PLUGIN_ARCHIVE_UNWRITABLE";

    /// <summary>
    /// The plugin id does not meet the plugin id rule (lower-case letters, digits and single
    /// hyphens) or would resolve outside the plugins folder (400).
    /// </summary>
    public const string PLUGIN_ID_INVALID = "PLUGIN_ID_INVALID";

    /// <summary>No plugin with that id is installed (404).</summary>
    public const string PLUGIN_NOT_FOUND = "PLUGIN_NOT_FOUND";

    /// <summary>The archive has more entries, or unpacks to more bytes, than this host accepts (413).</summary>
    public const string PLUGIN_ARCHIVE_TOO_LARGE = "PLUGIN_ARCHIVE_TOO_LARGE";

    /// <summary>
    /// The archive carries an assembly named like one of the host's own or the framework's, which
    /// would be loaded in place of the host's copy (400).
    /// </summary>
    public const string PLUGIN_HOST_ASSEMBLY_REFUSED = "PLUGIN_HOST_ASSEMBLY_REFUSED";

    /// <summary>
    /// The archive carries an assembly whose file name (compared without regard to case) another installed or staged
    /// plugin already ships, which would make one plugin run on the other's copy (400).
    /// </summary>
    public const string PLUGIN_ASSEMBLY_CONFLICT = "PLUGIN_ASSEMBLY_CONFLICT";

    /// <summary>A trusted-package list is configured and the archive is not on it (400).</summary>
    public const string PLUGIN_NOT_TRUSTED = "PLUGIN_NOT_TRUSTED";

    /// <summary>The plugin package carries no <c>plugin.sig</c> and the host does not admit unsigned packages (400).</summary>
    public const string PLUGIN_UNSIGNED = "PLUGIN_UNSIGNED";

    /// <summary>The plugin package's <c>plugin.sig</c> cannot be read as a signature (400).</summary>
    public const string PLUGIN_SIGNATURE_MALFORMED = "PLUGIN_SIGNATURE_MALFORMED";

    /// <summary>The plugin package is signed with a key that this host does not trust (400).</summary>
    public const string PLUGIN_SIGNATURE_KEY_UNKNOWN = "PLUGIN_SIGNATURE_KEY_UNKNOWN";

    /// <summary>The plugin package's signature does not match its files, so it was changed after signing (400).</summary>
    public const string PLUGIN_SIGNATURE_INVALID = "PLUGIN_SIGNATURE_INVALID";

    /// <summary>The plugin ships with this build and cannot be removed or replaced by an upload (409).</summary>
    public const string PLUGIN_BUILT_IN = "PLUGIN_BUILT_IN";

    /// <summary>The upload is an older version than the installed plugin and the operator did not force the downgrade (409).</summary>
    public const string PLUGIN_DOWNGRADE_REFUSED = "PLUGIN_DOWNGRADE_REFUSED";

    /// <summary>The plugin's package is still installed, so its stored data cannot be purged; remove the plugin first (409).</summary>
    public const string PLUGIN_STILL_INSTALLED = "PLUGIN_STILL_INSTALLED";

    // Plugin routes

    /// <summary>
    /// A plugin route was called that is not served: the plugin is unknown, is not a serving plugin, is switched off or
    /// not configured, or does not declare that method and path in that area. One answer for all of them, so the
    /// route cannot be probed (404).
    /// </summary>
    public const string PLUGIN_ROUTE_NOT_FOUND = "PLUGIN_ROUTE_NOT_FOUND";

    /// <summary>
    /// The plugin threw, or broke its contract (no reply, a malformed problem code, an unknown reply kind). Nothing of
    /// the failure reaches the caller; the host logs it with the plugin id (502).
    /// </summary>
    public const string PLUGIN_FAILED = "PLUGIN_FAILED";

    /// <summary>A plugin did not answer within the call time limit (<c>PluginApi:CallTimeoutSeconds</c>); the call was cancelled and nothing of the plugin reaches the caller (504).</summary>
    public const string PLUGIN_TIMEOUT = "PLUGIN_TIMEOUT";

    /// <summary>A plugin passed a store key the SDK does not allow; answered to the caller as <see cref="PLUGIN_FAILED"/> (502).</summary>
    public const string PLUGIN_STORE_KEY_INVALID = Domain.Plugins.PluginStoreErrors.KeyInvalid;

    /// <summary>A plugin passed a missing or oversized store value; answered to the caller as <see cref="PLUGIN_FAILED"/> (502).</summary>
    public const string PLUGIN_STORE_VALUE_INVALID = Domain.Plugins.PluginStoreErrors.ValueInvalid;

    /// <summary>
    /// A plugin's write would take its store scope past the configured number of keys
    /// (<c>PluginApi:MaxStoreKeysPerScope</c>) or characters (<c>PluginApi:MaxStoreCharsPerScope</c>) (402).
    /// </summary>
    public const string PLUGIN_STORE_QUOTA_EXCEEDED = Domain.Plugins.PluginStoreErrors.QuotaExceeded;

    /// <summary>The request body is larger than the host passes to a plugin (<c>PluginApi:MaxBodyBytes</c>), checked before the plugin is called (413).</summary>
    public const string PLUGIN_REQUEST_TOO_LARGE = "PLUGIN_REQUEST_TOO_LARGE";

    /// <summary>
    /// The sentence a plugin's problem carries when the plugin gave none. Never the <c>type</c> of a response: a
    /// plugin's own codes are namespaced under <c>plugin.{id}.</c> and so can never equal a host code.
    /// </summary>
    public const string PLUGIN_REPORTED_PROBLEM = "PLUGIN_REPORTED_PROBLEM";

    /// <summary>
    /// A plugin slot was asked for that the host does not offer in that area: no such slot id, or a slot of the other
    /// panel. One answer for both (404).
    /// </summary>
    public const string PLUGIN_SLOT_NOT_FOUND = "PLUGIN_SLOT_NOT_FOUND";

    /// <summary>
    /// A client slot widget's view is not served to this caller: the slot, the context, the widget or its view does not
    /// exist or is not the caller's to see. One answer for all of them (404), so the endpoint is no oracle. Not
    /// <see cref="PLUGIN_SLOT_WIDGET_NOT_FOUND"/>, the operator's binding refusal, whose sentence names the plugin.
    /// </summary>
    public const string PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND = "PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND";

    /// <summary>The slot needs a context id (a service or a client) and the request named none (400).</summary>
    public const string PLUGIN_SLOT_CONTEXT_REQUIRED = "PLUGIN_SLOT_CONTEXT_REQUIRED";

    /// <summary>The plugin declares no widget with that id in that slot, so there is nothing to bind products to (404).</summary>
    public const string PLUGIN_SLOT_WIDGET_NOT_FOUND = "PLUGIN_SLOT_WIDGET_NOT_FOUND";

    /// <summary>
    /// Every product id sent to bind a widget to is unknown or deleted, so nothing would be bound. Refused (400) instead of
    /// clearing the binding, which only an explicit empty list does.
    /// </summary>
    public const string PLUGIN_SLOT_PRODUCTS_UNKNOWN = "PLUGIN_SLOT_PRODUCTS_UNKNOWN";

    // Admin: integrations

    /// <summary>The slug names neither a built-in integration nor a loaded plugin (404).</summary>
    public const string INTEGRATION_NOT_FOUND = "INTEGRATION_NOT_FOUND";

    /// <summary>A saved field key is not one the integration declares, or is the reserved switch key (400).</summary>
    public const string INTEGRATION_FIELD_UNKNOWN = "INTEGRATION_FIELD_UNKNOWN";

    /// <summary>A saved field value does not fit the declared type of the field (400); <c>errors[key]</c> says what is expected.</summary>
    public const string INTEGRATION_FIELD_INVALID = "INTEGRATION_FIELD_INVALID";

    /// <summary>The integration has no connection test: a plugin that does not implement <c>IPluginStatus</c> (400).</summary>
    public const string INTEGRATION_NOT_TESTABLE = "INTEGRATION_NOT_TESTABLE";

    /// <summary>Not a refusal: the sentence of a plugin integration that reports it works, shown on its card and in a connection test.</summary>
    public const string PLUGIN_STATUS_HEALTHY = "PLUGIN_STATUS_HEALTHY";

    /// <summary>Not a refusal: the sentence of a plugin integration that reports it works with reduced function.</summary>
    public const string PLUGIN_STATUS_DEGRADED = "PLUGIN_STATUS_DEGRADED";

    /// <summary>Not a refusal: the sentence of a plugin integration that reports it cannot do its work.</summary>
    public const string PLUGIN_STATUS_REPORTED_DOWN = "PLUGIN_STATUS_REPORTED_DOWN";

    /// <summary>Not a refusal: the plugin was not asked for its state, because it is switched off, misses a required setting or waits for its data migration.</summary>
    public const string PLUGIN_STATUS_INACTIVE = "PLUGIN_STATUS_INACTIVE";

    /// <summary>Not a refusal: the plugin did not answer its status check within the host's time limit.</summary>
    public const string PLUGIN_STATUS_TIMED_OUT = "PLUGIN_STATUS_TIMED_OUT";

    /// <summary>Not a refusal: the plugin's status check failed or gave no valid answer; the cause is in the server log.</summary>
    public const string PLUGIN_STATUS_FAILED = "PLUGIN_STATUS_FAILED";

    // Products

    /// <summary>The id names no product group (404).</summary>
    public const string PRODUCT_GROUP_NOT_FOUND = "PRODUCT_GROUP_NOT_FOUND";

    /// <summary>The id names no product specification line (404).</summary>
    public const string PRODUCT_FEATURE_NOT_FOUND = "PRODUCT_FEATURE_NOT_FOUND";

    /// <summary>
    /// The product already has a specification line under this label; the storefront's comparison
    /// table keys its rows on the label and would render only the first one (400).
    /// </summary>
    public const string PRODUCT_FEATURE_LABEL_DUPLICATE = "PRODUCT_FEATURE_LABEL_DUPLICATE";

    /// <summary>Another product already uses this SKU; a SKU names one product only (409).</summary>
    public const string PRODUCT_SKU_ALREADY_IN_USE = "PRODUCT_SKU_ALREADY_IN_USE";

    // Slides

    /// <summary>The id names no homepage slide (404).</summary>
    public const string SLIDE_NOT_FOUND = "SLIDE_NOT_FOUND";

    // Storefront content

    /// <summary>The id names no testimonial (404).</summary>
    public const string TESTIMONIAL_NOT_FOUND = "TESTIMONIAL_NOT_FOUND";

    /// <summary>The id names no partner (404).</summary>
    public const string PARTNER_NOT_FOUND = "PARTNER_NOT_FOUND";

    /// <summary>The id names no server location (404).</summary>
    public const string LOCATION_NOT_FOUND = "LOCATION_NOT_FOUND";

    /// <summary>The id names no portfolio project (404).</summary>
    public const string PROJECT_NOT_FOUND = "PROJECT_NOT_FOUND";

    /// <summary>The id names no frequently asked question (404).</summary>
    public const string FAQ_NOT_FOUND = "FAQ_NOT_FOUND";

    /// <summary>Another portfolio project already uses the slug (409, via the <c>_EXISTS</c> suffix).</summary>
    public const string PROJECT_SLUG_EXISTS = "PROJECT_SLUG_EXISTS";

    // Migration

    /// <summary>The id or key names no migration job (404).</summary>
    public const string MIGRATION_JOB_NOT_FOUND = "MIGRATION_JOB_NOT_FOUND";

    /// <summary>
    /// The job is not <c>Pending</c>, so a pull cannot be started for it: it is already running,
    /// already finished, or already failed (400).
    /// </summary>
    public const string MIGRATION_JOB_NOT_PENDING = "MIGRATION_JOB_NOT_PENDING";

    /// <summary>
    /// The job has not failed, so there is nothing to retry: it is pending, queued, running or
    /// completed, or a concurrent retry queued it first (400).
    /// </summary>
    public const string MIGRATION_JOB_NOT_FAILED = "MIGRATION_JOB_NOT_FAILED";

    /// <summary>The job's status does not allow the requested step (409).</summary>
    public const string MIGRATION_JOB_INVALID_TRANSITION = Domain.Migration.MigrationJobErrors.InvalidTransition;

    /// <summary>
    /// The migration plugin's files are not present on this deployment, so there is nothing to
    /// download (404).
    /// </summary>
    public const string MIGRATION_PLUGIN_NOT_FOUND = "MIGRATION_PLUGIN_NOT_FOUND";

    /// <summary>
    /// The source system, a port this deployment does not own, could not be reached or refused
    /// the connection test (502). No detail from the source ever reaches the response.
    /// </summary>
    public const string MIGRATION_CONNECTION_UPSTREAM_FAILED = "MIGRATION_CONNECTION_UPSTREAM_FAILED";

    // Admin: settings

    /// <summary>The id names no configuration setting (404).</summary>
    public const string SETTING_NOT_FOUND = "SETTING_NOT_FOUND";

    /// <summary>
    /// The written value is outside the key's fixed vocabulary, or does not match the shape the
    /// key requires (400). <c>detail</c> carries a fixed sentence; the per-key wording, the
    /// values or the shape actually accepted, travels in <c>errors[key]</c> instead, because it
    /// depends on the row being written and cannot be a single resx sentence.
    /// </summary>
    public const string INVALID_SETTING_VALUE = "INVALID_SETTING_VALUE";

    /// <summary>
    /// The general settings endpoints refuse to write this row: its key is outside the settings catalogue
    /// (an integration field or switch, the setup token or a deployment key) (400). Integrations are saved
    /// through their own endpoint.
    /// </summary>
    public const string SETTING_NOT_EDITABLE = "SETTING_NOT_EDITABLE";

    // Admin: branding

    /// <summary>
    /// The uploaded bytes are not an image this deployment will store, not an image at all, an
    /// unsupported format, or a declared type that disagrees with the real bytes (400). The
    /// precise cause is logged, never returned.
    /// </summary>
    public const string BRANDING_IMAGE_INVALID = "BRANDING_IMAGE_INVALID";

    /// <summary>
    /// The upload is over the byte ceiling, or decodes to more pixels than this deployment will
    /// rasterise (413, via the <c>_TOO_LARGE</c> suffix). Which ceiling was hit is logged, never
    /// returned.
    /// </summary>
    public const string BRANDING_IMAGE_TOO_LARGE = "BRANDING_IMAGE_TOO_LARGE";

    // Auth: account provisioning refused by an admin action

    /// <summary>
    /// A guest checkout, an admin "create client" or a migration pull tried to create an account
    /// where an external sign-on provider owns the accounts (400). Answered from <c>PlaceOrderHandler</c>'s guest-checkout
    /// branch and <c>AdminCreateClientHandler</c>'s new-account branch; a migration pull logs the
    /// same refusal against the job's own log instead of answering an HTTP caller.
    /// </summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT = "USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT";

    /// <summary>
    /// An admin tried to rename a person where an external sign-on provider owns the accounts (400). Answered from
    /// <c>UpdateUserHandler</c>'s own call.
    /// </summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_CHANGE_NAME = "USER_PROVISIONING_NOT_ALLOWED_CHANGE_NAME";

    /// <summary>
    /// An admin tried to change a person's sign-in address where an external sign-on provider owns the accounts (400).
    /// Answered from <c>UpdateUserHandler</c> and from <c>UpdateClientHandler</c>'s own call to the
    /// same provisioning method.
    /// </summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL = "USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL";

    /// <summary>An admin tried to delete an account where an external sign-on provider owns the accounts (400).</summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_DELETE_ACCOUNT = "USER_PROVISIONING_NOT_ALLOWED_DELETE_ACCOUNT";

    /// <summary>An admin tried to set a person's password directly where an external sign-on provider owns the accounts (400).</summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_SET_PASSWORD = "USER_PROVISIONING_NOT_ALLOWED_SET_PASSWORD";

    /// <summary>
    /// An admin tried to issue a password-reset link where an external sign-on provider owns the accounts (400).
    /// </summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_ISSUE_PASSWORD_RESET =
        "USER_PROVISIONING_NOT_ALLOWED_ISSUE_PASSWORD_RESET";

    /// <summary>
    /// Something tried to confirm an email address where an external sign-on provider owns the accounts (400).
    /// </summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_CONFIRM_EMAIL =
        "USER_PROVISIONING_NOT_ALLOWED_CONFIRM_EMAIL";

    /// <summary>
    /// Something tried to issue an email-confirmation token where an external sign-on provider owns the accounts (400).
    /// </summary>
    public const string USER_PROVISIONING_NOT_ALLOWED_ISSUE_EMAIL_CONFIRMATION =
        "USER_PROVISIONING_NOT_ALLOWED_ISSUE_EMAIL_CONFIRMATION";

    // Domain aggregate refusals. The literal string is declared once, on the
    // Domain-side <c>&lt;Feature&gt;Errors</c> class each aggregate throws with: Domain may not
    // reference this Application-layer class, so these constants alias that value rather than
    // duplicating it, purely so ResourceCoverageTests (which reflects over this class only) still
    // requires a sentence for each one in every language.

    /// <summary>The base currency cannot be disabled (400): <see cref="Domain.Billing.Currency.Disable"/>.</summary>
    public const string CURRENCY_BASE_DISABLE_NOT_ALLOWED = Domain.Billing.BillingErrors.CurrencyBaseDisableNotAllowed;

    /// <summary>Only a Draft invoice may be published (400): <see cref="Domain.Billing.Invoice.Publish"/>.</summary>
    public const string INVOICE_NOT_DRAFT = Domain.Billing.BillingErrors.InvoiceNotDraft;

    /// <summary>The invoice is no longer editable (400): <see cref="Domain.Billing.Invoice.AddItem"/>.</summary>
    public const string INVOICE_NOT_EDITABLE = Domain.Billing.BillingErrors.InvoiceNotEditable;

    /// <summary>
    /// The id names no line item on this invoice (404): <see cref="Domain.Billing.Invoice.RemoveItem"/>
    /// and <see cref="Domain.Billing.Invoice.UpdateItem"/>.
    /// </summary>
    public const string INVOICE_ITEM_NOT_FOUND = Domain.Billing.BillingErrors.InvoiceItemNotFound;

    /// <summary>
    /// The invoice is not eligible to be marked overdue (400): <see cref="Domain.Billing.Invoice.MarkOverdue"/>.
    /// </summary>
    public const string INVOICE_NOT_UNPAID = Domain.Billing.BillingErrors.InvoiceNotUnpaid;

    /// <summary>
    /// No manual bank transfer is waiting on the invoice, so there is nothing to confirm or reject; also the answer to a
    /// second confirm of the same transfer (409): <see cref="Domain.Billing.Invoice.ConfirmBankTransfer"/>.
    /// </summary>
    public const string INVOICE_BANK_TRANSFER_NOT_PENDING = Domain.Billing.BillingErrors.InvoiceBankTransferNotPending;

    /// <summary>The admin has not switched manual bank transfer on, so the client cannot choose it (409).</summary>
    public const string BANK_TRANSFER_NOT_AVAILABLE = "BANK_TRANSFER_NOT_AVAILABLE";

    /// <summary>
    /// The invoice is not eligible to be marked lapsed (400): <see cref="Domain.Billing.Invoice.MarkLapsed"/>
    /// requires Overdue.
    /// </summary>
    public const string INVOICE_NOT_OVERDUE = Domain.Billing.BillingErrors.InvoiceNotOverdue;

    /// <summary>A paid, refunded or already cancelled invoice cannot be cancelled (400): <see cref="Domain.Billing.Invoice.Cancel"/>.</summary>
    public const string INVOICE_CANCEL_NOT_ALLOWED = Domain.Billing.BillingErrors.InvoiceCancelNotAllowed;

    /// <summary>
    /// The invoice must be Paid for this action (400): <see cref="Domain.Billing.Invoice.MarkUnpaid"/>;
    /// or Paid or PartiallyRefunded for a refund: <see cref="Domain.Billing.Invoice.Refund"/>.
    /// </summary>
    public const string INVOICE_NOT_PAID = Domain.Billing.BillingErrors.InvoiceNotPaid;

    /// <summary>The quote is no longer editable (400): <see cref="Domain.Billing.Quote.AddItem"/>.</summary>
    public const string QUOTE_NOT_EDITABLE = Domain.Billing.BillingErrors.QuoteNotEditable;

    /// <summary>The quote's stage does not allow acceptance (400): <see cref="Domain.Billing.Quote.Accept"/>.</summary>
    public const string QUOTE_NOT_ACCEPTABLE = Domain.Billing.BillingErrors.QuoteNotAcceptable;

    /// <summary>
    /// The id names no line item on this quote (404): <see cref="Domain.Billing.Quote.RemoveItem"/>
    /// and <see cref="Domain.Billing.Quote.UpdateItem"/>.
    /// </summary>
    public const string QUOTE_ITEM_NOT_FOUND = Domain.Billing.BillingErrors.QuoteItemNotFound;

    /// <summary>
    /// A transaction moves no money in either direction (422): <see cref="Domain.Billing.Transaction.Create"/>
    /// and <see cref="Domain.Billing.Transaction.CorrectAmounts"/>.
    /// </summary>
    public const string TRANSACTION_AMOUNT_REQUIRED = Domain.Billing.BillingErrors.TransactionAmountRequired;

    /// <summary>
    /// The entry is part of the client's credit history and cannot be edited or deleted (409):
    /// <see cref="Domain.Billing.Transaction.EnsureEditable"/>.
    /// </summary>
    public const string TRANSACTION_CREDIT_LOCKED = Domain.Billing.BillingErrors.TransactionCreditLocked;

    /// <summary>A credit top-up invoice is one fixed line and cannot be edited or taxed (400).</summary>
    public const string INVOICE_TOP_UP_FIXED = Domain.Billing.BillingErrors.InvoiceTopUpFixed;

    /// <summary>Credit cannot be applied to a credit top-up invoice (400).</summary>
    public const string INVOICE_TOP_UP_NO_CREDIT = Domain.Billing.BillingErrors.InvoiceTopUpNoCredit;

    /// <summary>A paid credit top-up is refunded by staff by hand only (400).</summary>
    public const string INVOICE_TOP_UP_REFUND_MANUAL_ONLY = Domain.Billing.BillingErrors.InvoiceTopUpRefundManualOnly;

    /// <summary>The top-up amount is below the minimum or above the maximum the admin allows (400).</summary>
    public const string CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE = "CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE";

    /// <summary>The client already holds as many unpaid top-up invoices as the admin allows (409).</summary>
    public const string CREDIT_TOP_UP_TOO_MANY_OPEN = "CREDIT_TOP_UP_TOO_MANY_OPEN";

    /// <summary>The id names no credit note, or none the caller may see (404).</summary>
    public const string CREDIT_NOTE_NOT_FOUND = Domain.Billing.BillingErrors.CreditNoteNotFound;

    /// <summary>A credit note cannot be issued on a credit top-up invoice, which is credit already (400).</summary>
    public const string CREDIT_NOTE_TOP_UP_INVOICE = Domain.Billing.BillingErrors.CreditNoteTopUpInvoice;

    /// <summary>The credit note is more than the invoice can still be credited once earlier notes and refunds are counted (400).</summary>
    public const string CREDIT_NOTE_EXCEEDS_CREDITABLE = Domain.Billing.BillingErrors.CreditNoteExceedsCreditable;

    /// <summary>A credit note needs at least one valid line and a reason (400).</summary>
    public const string CREDIT_NOTE_ITEMS_REQUIRED = Domain.Billing.BillingErrors.CreditNoteItemsRequired;

    /// <summary>The credit note is not in the state the action needs (409).</summary>
    public const string CREDIT_NOTE_INVALID_STATE = Domain.Billing.BillingErrors.CreditNoteInvalidState;

    /// <summary>The credit note was already voided (409).</summary>
    public const string CREDIT_NOTE_ALREADY_VOIDED = Domain.Billing.BillingErrors.CreditNoteAlreadyVoided;

    /// <summary>A credit note paid out as a gateway refund cannot be voided, because the refund cannot be taken back (409).</summary>
    public const string CREDIT_NOTE_REFUND_NOT_VOIDABLE = Domain.Billing.BillingErrors.CreditNoteRefundNotVoidable;

    /// <summary>Account credit cannot be given for an invoice in a currency other than the one the client balance is kept in (400).</summary>
    public const string CREDIT_NOTE_CURRENCY_MISMATCH = "CREDIT_NOTE_CURRENCY_MISMATCH";

    /// <summary>
    /// The id names no contact on this client (404): <see cref="Domain.Clients.Client.RemoveContact"/>
    /// and <see cref="Domain.Clients.Client.UpdateContact"/>. The one answer for a contact that is
    /// another account's and one that does not exist, on the admin and the client routes alike.
    /// </summary>
    public const string CLIENT_CONTACT_NOT_FOUND = Domain.Clients.ClientsErrors.ClientContactNotFound;

    /// <summary>
    /// The account owner cannot also be added as an additional user (400):
    /// <see cref="Domain.Clients.Client.AddUser"/>.
    /// </summary>
    public const string CLIENT_USER_IS_OWNER = Domain.Clients.ClientsErrors.ClientUserIsOwner;

    /// <summary>The user is already linked to this client (400): <see cref="Domain.Clients.Client.AddUser"/>.</summary>
    public const string CLIENT_USER_LINK_DUPLICATE = Domain.Clients.ClientsErrors.ClientUserLinkDuplicate;

    /// <summary>
    /// The user is not linked to this client (400): <see cref="Domain.Clients.Client.RemoveUser"/> and
    /// <see cref="Domain.Clients.Client.UpdateUserPermissions"/>.
    /// </summary>
    public const string CLIENT_USER_NOT_LINKED = Domain.Clients.ClientsErrors.ClientUserNotLinked;

    /// <summary>
    /// The signed-in user lacks the permission a client portal action needs, or has no account
    /// relationship at all (403, an exact <c>StatusMapping</c> row). Raised by the permission filter.
    /// </summary>
    public const string CLIENT_PERMISSION_DENIED = Domain.Clients.ClientsErrors.ClientPermissionDenied;

    /// <summary>
    /// The named user is already the account owner (400): <see cref="Domain.Clients.Client.TransferOwnership"/>.
    /// </summary>
    public const string CLIENT_TRANSFER_SAME_OWNER = Domain.Clients.ClientsErrors.ClientTransferSameOwner;

    /// <summary>
    /// The deduction exceeds the client's credit balance (400): <see cref="Domain.Clients.Client.DeductCredit"/>.
    /// </summary>
    public const string CLIENT_CREDIT_INSUFFICIENT = Domain.Clients.ClientsErrors.ClientCreditInsufficient;

    /// <summary>The client holds a credit balance, so the billing currency cannot change (409): <see cref="Domain.Clients.Client.SetCurrency"/>.</summary>
    public const string CLIENT_CURRENCY_LOCKED_BY_CREDIT = Domain.Clients.ClientsErrors.ClientCurrencyLockedByCredit;

    /// <summary>The account is not in a state that can be suspended (400): <see cref="Domain.Clients.Client.Suspend"/>.</summary>
    public const string CLIENT_SUSPEND_NOT_ALLOWED = Domain.Clients.ClientsErrors.ClientSuspendNotAllowed;

    /// <summary>The account is already closed (400): <see cref="Domain.Clients.Client.Close"/>.</summary>
    public const string CLIENT_CLOSE_NOT_ALLOWED = Domain.Clients.ClientsErrors.ClientCloseNotAllowed;

    /// <summary>A closed account cannot be reactivated this way (400): <see cref="Domain.Clients.Client.Activate"/>.</summary>
    public const string CLIENT_ACTIVATE_NOT_ALLOWED = Domain.Clients.ClientsErrors.ClientActivateNotAllowed;

    /// <summary>The account is suspended or closed and cannot be deactivated (400): <see cref="Domain.Clients.Client.Deactivate"/>.</summary>
    public const string CLIENT_DEACTIVATE_NOT_ALLOWED = Domain.Clients.ClientsErrors.ClientDeactivateNotAllowed;

    /// <summary>
    /// The account is suspended, so it may not place orders or pay through a new card method (403, an exact
    /// <c>StatusMapping</c> row).
    /// </summary>
    public const string CLIENT_SUSPENDED = Domain.Clients.ClientsErrors.ClientSuspended;

    /// <summary>
    /// The account is closed, so it may not place orders, pay through a new card method or be billed (403, an exact
    /// <c>StatusMapping</c> row).
    /// </summary>
    public const string CLIENT_CLOSED = Domain.Clients.ClientsErrors.ClientClosed;

    /// <summary>
    /// The account has credit card processing disabled, so no card payment or saved card charge is taken from it
    /// (403, an exact <c>StatusMapping</c> row).
    /// </summary>
    public const string CLIENT_CARD_PAYMENT_DISABLED = Domain.Clients.ClientsErrors.ClientCardPaymentDisabled;

    /// <summary>
    /// The account does not allow single sign-on into the hosting panels of its services (403, an exact
    /// <c>StatusMapping</c> row).
    /// </summary>
    public const string CLIENT_SSO_DISABLED = Domain.Clients.ClientsErrors.ClientSsoDisabled;

    /// <summary>
    /// The account is tax exempt, so a tax rate cannot be set on its invoices (409, an exact
    /// <c>StatusMapping</c> row).
    /// </summary>
    public const string CLIENT_TAX_EXEMPT = Domain.Clients.ClientsErrors.ClientTaxExempt;

    /// <summary>
    /// The account owner cannot be removed as an additional user (409, an exact <c>StatusMapping</c> row):
    /// <see cref="Domain.Clients.Client.RemoveUser"/>.
    /// </summary>
    public const string CLIENT_OWNER_CANNOT_BE_REMOVED = Domain.Clients.ClientsErrors.ClientOwnerCannotBeRemoved;

    /// <summary>
    /// Another contact of this client already uses the email address (409, an exact <c>StatusMapping</c> row):
    /// <see cref="Domain.Clients.Client.AddContact"/> and <see cref="Domain.Clients.Client.UpdateContact"/>.
    /// </summary>
    public const string CONTACT_EMAIL_IN_USE = Domain.Clients.ClientsErrors.ClientContactEmailInUse;

    /// <summary>
    /// The account is closed, so its contacts and users cannot be changed (409, an exact <c>StatusMapping</c> row).
    /// </summary>
    public const string CLIENT_RECORD_CLOSED = Domain.Clients.ClientsErrors.ClientRecordClosed;

    /// <summary>
    /// The domain's status does not allow cancellation (400): <see cref="Domain.Domains.RegisteredDomain.Cancel"/>.
    /// </summary>
    public const string DOMAIN_CANCEL_NOT_ALLOWED = Domain.Domains.DomainsErrors.DomainCancelNotAllowed;

    /// <summary>
    /// The domain's status does not allow renewal (400): <see cref="Domain.Domains.RegisteredDomain.Renew"/>.
    /// </summary>
    public const string DOMAIN_RENEW_NOT_ALLOWED = Domain.Domains.DomainsErrors.DomainRenewNotAllowed;

    /// <summary>
    /// The domain's status does not allow linking a hosting service (400):
    /// <see cref="Domain.Domains.RegisteredDomain.LinkService"/>.
    /// </summary>
    public const string DOMAIN_LINK_NOT_ALLOWED = Domain.Domains.DomainsErrors.DomainLinkNotAllowed;

    /// <summary>
    /// The id names no DNS record on this domain (404):
    /// <see cref="Domain.Domains.RegisteredDomain.UpdateDnsRecord"/> and
    /// <see cref="Domain.Domains.RegisteredDomain.RemoveDnsRecord"/>.
    /// </summary>
    public const string DOMAIN_DNS_RECORD_NOT_FOUND = Domain.Domains.DomainsErrors.DomainDnsRecordNotFound;

    /// <summary>
    /// The id names no email forwarding rule on this domain (404):
    /// <see cref="Domain.Domains.RegisteredDomain.UpdateForwardingRule"/> and
    /// <see cref="Domain.Domains.RegisteredDomain.RemoveForwardingRule"/>.
    /// </summary>
    public const string DOMAIN_FORWARDING_RULE_NOT_FOUND = Domain.Domains.DomainsErrors.DomainForwardingRuleNotFound;

    /// <summary>
    /// The order is not Pending (400): <see cref="Domain.Orders.Order.Accept"/>,
    /// <see cref="Domain.Orders.Order.Cancel"/> and <see cref="Domain.Orders.Order.MarkFraud"/>.
    /// </summary>
    public const string ORDER_NOT_PENDING = Domain.Orders.OrdersErrors.OrderNotPending;

    /// <summary>
    /// The order line may not be retried (409): the order was not accepted, the line did not fail, its cost
    /// was given back, or another retry is running it. <see cref="Domain.Orders.Order.StartLineRetry"/>.
    /// </summary>
    public const string ORDER_LINE_NOT_RETRYABLE = Domain.Orders.OrdersErrors.OrderLineNotRetryable;

    /// <summary>
    /// The id names no line of that order (404): the retry of an order line, and
    /// <see cref="Domain.Orders.Order.RecordLineFulfilled"/>.
    /// </summary>
    public const string ORDER_ITEM_NOT_FOUND = Domain.Orders.OrdersErrors.OrderItemNotFound;

    /// <summary>
    /// The order line is already fulfilled, so a retry would fulfil it twice (409). The line is left as it is.
    /// </summary>
    public const string ORDER_LINE_ALREADY_FULFILLED = "ORDER_LINE_ALREADY_FULFILLED";

    /// <summary>
    /// The order line changed while an attempt ran (409): another writer saved it first, or it is being or was
    /// refunded, so the outcome of the attempt could not be recorded. <see cref="Domain.Orders.Order.RecordLineFailed"/>.
    /// </summary>
    public const string ORDER_LINE_CHANGED = Domain.Orders.OrdersErrors.OrderLineChanged;

    /// <summary>
    /// The order line is not in the state a refund needs (409): only a failed line can start being refunded and
    /// only a line being refunded can be settled. <see cref="Domain.Orders.Order.BeginLineRefund"/>.
    /// </summary>
    public const string ORDER_LINE_NOT_REFUNDABLE = Domain.Orders.OrdersErrors.OrderLineNotRefundable;

    /// <summary>
    /// The code a line is recorded with when its attempt was cut short before it ended (the request was cancelled
    /// or the host stopped), so the line can be retried at once instead of waiting for the retry claim to run out.
    /// </summary>
    public const string ORDER_LINE_INTERRUPTED = "ORDER_LINE_INTERRUPTED";

    /// <summary>
    /// The SKU is not letters and digits joined by single hyphens, underscores or dots, or is too long
    /// (400): <see cref="Domain.Products.Product.SetSku"/>.
    /// </summary>
    public const string PRODUCT_SKU_INVALID = Domain.Products.ProductsErrors.ProductSkuInvalid;

    /// <summary>
    /// The product metadata is not a JSON object or is larger than
    /// <see cref="Domain.Products.Product.MaxMetadataBytes"/> (400): <see cref="Domain.Products.Product.SetMetadata"/>.
    /// </summary>
    public const string PRODUCT_METADATA_INVALID = Domain.Products.ProductsErrors.ProductMetadataInvalid;

    /// <summary>
    /// The grace period is outside the accepted range of days (400):
    /// <see cref="Domain.Products.Product.SetGracePeriodDays"/>.
    /// </summary>
    public const string PRODUCT_GRACE_PERIOD_OUT_OF_RANGE = Domain.Products.ProductsErrors.ProductGracePeriodOutOfRange;

    /// <summary>The service is not Active (400): <see cref="Domain.Services.ClientService.Suspend"/>.</summary>
    public const string SERVICE_NOT_ACTIVE = Domain.Services.ServicesErrors.ServiceNotActive;

    /// <summary>The service is already terminated (400): <see cref="Domain.Services.ClientService.Terminate"/>.</summary>
    public const string SERVICE_TERMINATE_NOT_ALLOWED = Domain.Services.ServicesErrors.ServiceTerminateNotAllowed;

    /// <summary>The service is not Suspended (400): <see cref="Domain.Services.ClientService.Unsuspend"/>.</summary>
    public const string SERVICE_NOT_SUSPENDED = Domain.Services.ServicesErrors.ServiceNotSuspended;

    /// <summary>Staff asked for a status the service cannot be moved to (400), when <see cref="Domain.Services.ClientService.CanChangeStatusTo"/> refuses it.</summary>
    public const string SERVICE_STATUS_CHANGE_NOT_ALLOWED = Domain.Services.ServicesErrors.ServiceStatusChangeNotAllowed;

    /// <summary>A hold date without the hold, or a termination reason without the end-of-cycle termination (400), from <see cref="Domain.Services.ClientService.ConfigureAutomation"/>.</summary>
    public const string SERVICE_AUTOMATION_INCONSISTENT = Domain.Services.ServicesErrors.ServiceAutomationInconsistent;

    /// <summary>The service is not terminated, so it has no termination date to correct (409), from <see cref="Domain.Services.ClientService.CorrectTerminationDate"/>.</summary>
    public const string SERVICE_NOT_TERMINATED = Domain.Services.ServicesErrors.ServiceNotTerminated;

    /// <summary>A closed ticket does not accept new replies (400): <see cref="Domain.Support.Ticket.AddReply"/>.</summary>
    public const string TICKET_REPLY_NOT_ALLOWED = Domain.Support.SupportErrors.TicketReplyNotAllowed;

    /// <summary>The ticket is already closed (400): <see cref="Domain.Support.Ticket.Close"/>.</summary>
    public const string TICKET_CLOSE_NOT_ALLOWED = Domain.Support.SupportErrors.TicketCloseNotAllowed;

    /// <summary>The sort order of a question is negative (400).</summary>
    public const string FAQ_SORT_ORDER_NEGATIVE = Domain.Faqs.FaqErrors.SortOrderNegative;

    /// <summary>The sort order of a partner is negative (400).</summary>
    public const string PARTNER_SORT_ORDER_NEGATIVE = Domain.Partners.PartnerErrors.SortOrderNegative;

    /// <summary>The sort order of a server location is negative (400).</summary>
    public const string LOCATION_SORT_ORDER_NEGATIVE = Domain.Locations.ServerLocationErrors.SortOrderNegative;

    /// <summary>The country code of a server location is not two letters (400).</summary>
    public const string LOCATION_COUNTRY_CODE_INVALID = Domain.Locations.ServerLocationErrors.CountryCodeInvalid;

    /// <summary>The sort order of a project is negative (400).</summary>
    public const string PROJECT_SORT_ORDER_NEGATIVE = Domain.Projects.ProjectErrors.SortOrderNegative;

    /// <summary>A project's slug is empty (400).</summary>
    public const string PROJECT_SLUG_REQUIRED = Domain.Projects.ProjectErrors.SlugRequired;

    /// <summary>The sort order of a testimonial is negative (400).</summary>
    public const string TESTIMONIAL_SORT_ORDER_NEGATIVE = Domain.Testimonials.TestimonialErrors.SortOrderNegative;

    /// <summary>A testimonial's name is empty (400).</summary>
    public const string TESTIMONIAL_NAME_REQUIRED = Domain.Testimonials.TestimonialErrors.NameRequired;

    /// <summary>A testimonial's rating is outside 1 to 5 (400).</summary>
    public const string TESTIMONIAL_RATING_OUT_OF_RANGE = Domain.Testimonials.TestimonialErrors.RatingOutOfRange;

    /// <summary>The refund is more than what was paid and not yet refunded (400): <see cref="Domain.Billing.Invoice.AddRefund"/>.</summary>
    public const string INVOICE_REFUND_EXCEEDS_PAID = Domain.Billing.BillingErrors.InvoiceRefundExceedsPaid;

    /// <summary>Another refund of the invoice is still waiting for its gateway (409): <see cref="Domain.Billing.Invoice.BeginRefund"/>.</summary>
    public const string INVOICE_REFUND_CONFLICT = Domain.Billing.BillingErrors.InvoiceRefundConflict;

    /// <summary>The credit is more than the invoice still owes (400): <see cref="Domain.Billing.Invoice.ApplyCredit"/>.</summary>
    public const string INVOICE_CREDIT_EXCEEDS_DUE = Domain.Billing.BillingErrors.InvoiceCreditExceedsDue;

    /// <summary>The payment is more than the invoice still owes (400): <see cref="Domain.Billing.Invoice.AddPayment"/>.</summary>
    public const string INVOICE_PAYMENT_EXCEEDS_DUE = Domain.Billing.BillingErrors.InvoicePaymentExceedsDue;

    /// <summary>The invoice still owes money and must be settled, not marked paid (400): <see cref="Domain.Billing.Invoice.MarkPaid"/>.</summary>
    public const string INVOICE_AMOUNT_STILL_DUE = Domain.Billing.BillingErrors.InvoiceAmountStillDue;

    /// <summary>
    /// The invoice already took a payment and cannot be cancelled, reopened or closed as history (400);
    /// see <see cref="Domain.Billing.Invoice.EnsureCancellable"/> and <see cref="Domain.Billing.Invoice.MarkUnpaid"/>.
    /// </summary>
    public const string INVOICE_HAS_PAYMENTS = Domain.Billing.BillingErrors.InvoiceHasPayments;

    /// <summary>The change would make the invoice charge less than the payments it already took (400); see <see cref="Domain.Billing.Invoice.RemoveItem"/>.</summary>
    public const string INVOICE_TOTAL_BELOW_PAID = Domain.Billing.BillingErrors.InvoiceTotalBelowPaid;

    /// <summary>A gateway refund was asked for an invoice no payment gateway took money for (400).</summary>
    public const string INVOICE_NOT_GATEWAY_PAID = "INVOICE_NOT_GATEWAY_PAID";

    /// <summary>The hosted payment gateway refused or failed a refund (502); nothing was returned through it.</summary>
    public const string GATEWAY_REFUND_REFUSED = "GATEWAY_REFUND_REFUSED";

    /// <summary>A fulfilment plugin enabled on this deployment could not say how much may be refunded, because it threw or did not answer in time (502); nothing was refunded.</summary>
    public const string REFUND_ADVICE_UNAVAILABLE = "REFUND_ADVICE_UNAVAILABLE";

    /// <summary>A fulfilment plugin allows no refund of this invoice at the moment (409); nothing was refunded.</summary>
    public const string REFUND_BLOCKED_BY_PLUGIN = "REFUND_BLOCKED_BY_PLUGIN";

    /// <summary>The gateway reported a paid amount or currency other than what the invoice owes (400); the invoice stays unpaid.</summary>
    public const string GATEWAY_PAYMENT_MISMATCH = "GATEWAY_PAYMENT_MISMATCH";

    /// <summary>Only a Draft or Cancelled invoice may be deleted (400): <see cref="Domain.Billing.Invoice.CanDelete"/>.</summary>
    public const string INVOICE_NOT_DELETABLE = "INVOICE_NOT_DELETABLE";

    /// <summary>The payment gateway module is not installed, is not enabled, or its plugin does not offer stored credentials (409).</summary>
    public const string STORED_CREDENTIALS_NOT_SUPPORTED = "STORED_CREDENTIALS_NOT_SUPPORTED";

    /// <summary>The client holds no active consent for a stored payment method, so none may be charged (400).</summary>
    public const string CARD_CONSENT_REQUIRED = "CARD_CONSENT_REQUIRED";

    /// <summary>The id names no stored charge consent of the caller (404).</summary>
    public const string CARD_CONSENT_NOT_FOUND = "CARD_CONSENT_NOT_FOUND";

    /// <summary>The payer did not finish saving a payment method: the gateway session was abandoned or expired (400).</summary>
    public const string STORED_METHOD_NOT_SAVED = "STORED_METHOD_NOT_SAVED";

    /// <summary>The invoice already has a stored charge in flight, so no second one starts (409).</summary>
    public const string STORED_CHARGE_ALREADY_IN_PROGRESS = "STORED_CHARGE_ALREADY_IN_PROGRESS";

    /// <summary>The id names no stored charge claim (404).</summary>
    public const string INVOICE_PAYMENT_CLAIM_NOT_FOUND = "INVOICE_PAYMENT_CLAIM_NOT_FOUND";

    /// <summary>The invoice has a stored card charge that has not finished (in flight, or waiting for the payer or the gateway), so it cannot be cancelled or paid by hand yet, and no hosted payment can start (409).</summary>
    public const string INVOICE_STORED_CHARGE_UNRESOLVED = "INVOICE_STORED_CHARGE_UNRESOLVED";

    /// <summary>The invoice has a stored card charge held for an operator (the gateway took money the invoice could not record), so no hosted payment can start until an operator settles it by a manual payment or by cancelling the invoice (409).</summary>
    public const string INVOICE_STORED_CHARGE_HELD = "INVOICE_STORED_CHARGE_HELD";

    /// <summary>The consent already covers a different payment method than the one the finished save session stored (409).</summary>
    public const string CARD_CONSENT_ALREADY_ATTACHED = "CARD_CONSENT_ALREADY_ATTACHED";

    // Plugin registry

    /// <summary>The plugin registry is switched off or has no address in this deployment's configuration (409).</summary>
    public const string PLUGIN_REGISTRY_DISABLED = "PLUGIN_REGISTRY_DISABLED";

    /// <summary>The plugin registry could not be reached or answered with an error status; nothing of the failure is passed on (502).</summary>
    public const string PLUGIN_REGISTRY_UNAVAILABLE = "PLUGIN_REGISTRY_UNAVAILABLE";

    /// <summary>The plugin registry did not answer within the configured time limit (504).</summary>
    public const string PLUGIN_REGISTRY_TIMED_OUT = "PLUGIN_REGISTRY_TIMED_OUT";

    /// <summary>The plugin registry answered with something that is not a well-formed reply (502).</summary>
    public const string PLUGIN_REGISTRY_RESPONSE_INVALID = "PLUGIN_REGISTRY_RESPONSE_INVALID";

    /// <summary>The registry lists no such plugin or no such version of it (404).</summary>
    public const string PLUGIN_REGISTRY_PLUGIN_NOT_FOUND = "PLUGIN_REGISTRY_PLUGIN_NOT_FOUND";

    /// <summary>
    /// The registry's package address points away from the registry's host, redirects elsewhere, or the package is over
    /// the download limit or is not the plugin asked for (502).
    /// </summary>
    public const string PLUGIN_REGISTRY_PACKAGE_REFUSED = "PLUGIN_REGISTRY_PACKAGE_REFUSED";

    /// <summary>The downloaded package does not match the SHA-256 the registry published for it (502).</summary>
    public const string PLUGIN_REGISTRY_CHECKSUM_MISMATCH = "PLUGIN_REGISTRY_CHECKSUM_MISMATCH";

    /// <summary>The operator's registry trust policy does not admit this plugin or the key it is signed with (403).</summary>
    public const string PLUGIN_REGISTRY_NOT_TRUSTED = "PLUGIN_REGISTRY_NOT_TRUSTED";

    /// <summary>The plugin is paid or private and the registry wants a licence key; none was sent (the registry answered 401).</summary>
    public const string PLUGIN_REGISTRY_LICENCE_REQUIRED = "PLUGIN_REGISTRY_LICENCE_REQUIRED";

    /// <summary>The registry refused the licence key that was sent (the registry answered 403).</summary>
    public const string PLUGIN_REGISTRY_LICENCE_INVALID = "PLUGIN_REGISTRY_LICENCE_INVALID";

    /// <summary>The version asked for does not run on this Hostpanel version, outside its minimum and maximum host version (409).</summary>
    public const string PLUGIN_REGISTRY_HOST_INCOMPATIBLE = "PLUGIN_REGISTRY_HOST_INCOMPATIBLE";

    /// <summary>The registry withdrew the version asked for (yanked), so it is not installed (409).</summary>
    public const string PLUGIN_REGISTRY_VERSION_YANKED = "PLUGIN_REGISTRY_VERSION_YANKED";

    /// <summary>The registry flagged the version asked for as compromised, so it is not installed (409).</summary>
    public const string PLUGIN_REGISTRY_VERSION_COMPROMISED = "PLUGIN_REGISTRY_VERSION_COMPROMISED";
}
