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

    /// <summary>The card gateway payment method id does not belong to the caller's account (400).</summary>
    public const string PAYMENT_METHOD_NOT_OWNED = "PAYMENT_METHOD_NOT_OWNED";

    /// <summary>
    /// The deployment cannot charge a saved payment method because it has no card gateway configured (409).
    /// </summary>
    public const string PAYMENT_METHOD_UNAVAILABLE = "PAYMENT_METHOD_UNAVAILABLE";

    /// <summary>The caller's account has no saved payment method yet (400).</summary>
    public const string NO_SAVED_PAYMENT_METHODS = "NO_SAVED_PAYMENT_METHODS";

    /// <summary>
    /// The gateway return URL is not an absolute URL, or its origin is not one this deployment
    /// is configured to hand a payer back to (400).
    /// </summary>
    public const string RETURN_URL_NOT_ALLOWED = "RETURN_URL_NOT_ALLOWED";

    /// <summary>The payment gateway declined or otherwise failed to charge the client (502).</summary>
    public const string GATEWAY_CHARGE_UPSTREAM_FAILED = "GATEWAY_CHARGE_UPSTREAM_FAILED";

    /// <summary>
    /// The card gateway could not be reached or failed on its side, so nothing was charged or
    /// nothing is known yet; the same attempt may be retried (503).
    /// </summary>
    public const string PAYMENT_GATEWAY_SERVICE_UNAVAILABLE = "PAYMENT_GATEWAY_SERVICE_UNAVAILABLE";

    /// <summary>
    /// The card gateway refused a saved-payment-method call (listing, changing the default,
    /// removing, or looking up a charge); repeating it unchanged will not help (502).
    /// </summary>
    public const string PAYMENT_METHOD_UPSTREAM_FAILED = "PAYMENT_METHOD_UPSTREAM_FAILED";

    /// <summary>
    /// The payment gateway reported the charge as successful but returned no transaction id to
    /// record against the invoice (502).
    /// </summary>
    public const string GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED = "GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED";

    /// <summary>
    /// The card payment intent does not pay this order's invoice: its amount, currency, order or
    /// invoice differs from what the invoice is due for (400).
    /// </summary>
    public const string PAYMENT_INTENT_MISMATCH = "PAYMENT_INTENT_MISMATCH";

    /// <summary>The card payment intent was already recorded as a payment (409).</summary>
    public const string PAYMENT_INTENT_ALREADY_USED = "PAYMENT_INTENT_ALREADY_USED";

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

    /// <summary>
    /// One of the archive's entries would write outside the plugin's own directory (400). The
    /// zip-slip guard's refusal, named for what a caller can act on rather than the attack it
    /// blocks.
    /// </summary>
    public const string PLUGIN_ARCHIVE_UNSAFE = "PLUGIN_ARCHIVE_UNSAFE";

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

    /// <summary>A trusted-package list is configured and the archive is not on it (400).</summary>
    public const string PLUGIN_NOT_TRUSTED = "PLUGIN_NOT_TRUSTED";

    /// <summary>The plugin ships with this build and cannot be removed or replaced by an upload (409).</summary>
    public const string PLUGIN_BUILT_IN = "PLUGIN_BUILT_IN";

    // Admin: integrations

    /// <summary>The slug names neither a built-in integration nor a loaded plugin (404).</summary>
    public const string INTEGRATION_NOT_FOUND = "INTEGRATION_NOT_FOUND";

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

    /// <summary>The card gateway refused a refund (502); any part it had already returned stays recorded.</summary>
    public const string CARD_REFUND_REFUSED = "CARD_REFUND_REFUSED";

    /// <summary>The hosted payment gateway refused or failed a refund (502); nothing was returned through it.</summary>
    public const string GATEWAY_REFUND_REFUSED = "GATEWAY_REFUND_REFUSED";

    /// <summary>The gateway reported a paid amount or currency other than what the invoice owes (400); the invoice stays unpaid.</summary>
    public const string GATEWAY_PAYMENT_MISMATCH = "GATEWAY_PAYMENT_MISMATCH";

    /// <summary>Only a Draft or Cancelled invoice may be deleted (400): <see cref="Domain.Billing.Invoice.CanDelete"/>.</summary>
    public const string INVOICE_NOT_DELETABLE = "INVOICE_NOT_DELETABLE";
}
