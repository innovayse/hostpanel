using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Shared.Controllers;

/// <summary>
/// The one place an error code becomes an HTTP status and a title. Exact codes first, then
/// tokens, first match wins, in that order.
/// </summary>
/// <remarks>
/// The generic arms (<c>RATE_LIMIT</c>, <c>_LOCKED</c>, <c>_UPSTREAM_FAILED</c>, hostpanel's
/// <c>_SEND_FAILED</c>/<c>_TOO_LARGE</c>/<c>_TOKEN_INVALID</c>, <c>_NOT_CONFIGURED</c>)
/// sit after every specific row, so <c>SHARE_NOT_FOUND_UPSTREAM_FAILED</c> is still 404 and
/// <c>VALIDATION_NOT_CONFIGURED</c> still 422. A code that merely contains <c>RATE_LIMIT</c> answers
/// 429: name new codes so they do not contain that token unless that status is meant.
/// </remarks>
public static class StatusMapping
{
    /// <summary>Maps an error code to the status and title the response carries.</summary>
    /// <param name="errorCode">The handler's code, SCREAMING_SNAKE.</param>
    /// <returns>The status and the title; 400 "Bad Request" for a code no row names.</returns>
    public static ErrorStatus For(string errorCode)
    {
        return errorCode switch
        {
            SharedErrorCodes.InvalidRequest => new(400, "Bad Request"),
            // Reserved for ExceptionHandlingMiddleware (FluentValidation → 400). Exact row above the
            // VALIDATION token so a stray handler use still answers 400; handler codes use
            // *_VALIDATION_FAILED → 422.
            SharedErrorCodes.ValidationError => new(400, "Validation Failed"),
            "QUOTA_EXCEEDED" or "LIMIT_EXCEEDED" => new(402, "Payment Required"),
            // Exact row: the first invoice must be paid before the client may set the service up.
            "SERVICE_FIRST_INVOICE_UNPAID" => new(402, "Payment Required"),
            // Exact row: the proration invoice of an upgrade must be paid before the plan changes.
            "SERVICE_CHANGE_INVOICE_UNPAID" => new(402, "Payment Required"),
            "PAYLOAD_TOO_LARGE" => new(413, "Content Too Large"),
            "CONTENT_REJECTED" => new(422, "Unprocessable Content"),
            "CONFIRMATION_REQUIRED" => new(409, "Conflict"),
            "CONCURRENCY_LIMITED" => new(429, "Too Many Requests"),
            "UPLOAD_FAILED" => new(502, "Bad Gateway"),
            // Exact row: the code names a policy, not a missing grant, so no FORBIDDEN token is in it.
            SharedErrorCodes.DemoActionDisabled => new(403, "Forbidden"),
            // Capacity, not configuration: naming it *_NOT_CONFIGURED would answer correctly by
            // accident and mislabel a full fleet as an unfinished deployment.
            "NO_ELIGIBLE_SERVER" => new(503, "Service Unavailable"),
            // Exact row: the panel could not be asked or its answer was lost, so the outcome is unknown and a retry
            // settles it; the name carries no token for a generic arm.
            "PROVISIONING_OUTCOME_UNKNOWN" => new(503, "Service Unavailable"),
            // Exact row: the order number sequence outgrew the configured number length, a state of
            // the deployment rather than of the request, and the name carries no token for it.
            "ORDER_NUMBER_RANGE_EXHAUSTED" => new(503, "Service Unavailable"),
            // Exact rows: the stored service or product is not ready to provision, a state conflict
            // rather than a malformed request, and neither name carries a CONFLICT token.
            "SERVICE_HOSTING_DETAILS_MISSING" or "CPANEL_USERNAME_INVALID" or "CPANEL_DOMAIN_INVALID" or "PRODUCT_PACKAGE_MISSING" or "PRODUCT_SERVER_GROUP_REQUIRED" => new(409, "Conflict"),
            // Exact rows: the campaign's or the recipient's state stands in the way of the action, a state conflict (409),
            // and none of the names carries a CONFLICT / ALREADY token for the generic arms.
            "CAMPAIGN_NOT_DRAFT" or "CAMPAIGN_NOT_SCHEDULED" or "CAMPAIGN_NOT_SENDING" or "CAMPAIGN_RECIPIENT_NOT_PENDING"
                => new(409, "Conflict"),
            // Exact row: only a terminated service has a termination date to correct, so the stored
            // status stands in the way (a state conflict), and the name carries no CONFLICT token.
            "SERVICE_NOT_TERMINATED" => new(409, "Conflict"),
            // Exact row: a status page group that still lists components cannot be removed, a state conflict whose name
            // carries no CONFLICT / ALREADY token for the generic arms.
            "STATUS_COMPONENT_GROUP_NOT_EMPTY" => new(409, "Conflict"),
            // Exact row: the plan change request is in a status that refuses the move, or the service left its plan since it was
            // made, a state conflict whose name carries no CONFLICT / ALREADY token for the generic arms.
            "SERVICE_CHANGE_INVALID_STATE" => new(409, "Conflict"),
            // A plugin shipped with the build: the request is well-formed, the target's state refuses it.
            "PLUGIN_BUILT_IN" => new(409, "Conflict"),
            // Exact row: a package whose assembly file name another plugin ships is a bad upload, a 400 like the host
            // assembly refusal; the CONFLICT in the name must not reach the generic arm below that answers 409.
            "PLUGIN_ASSEMBLY_CONFLICT" => new(400, "Bad Request"),
            // An upload older than the installed version, and a purge of data whose plugin is still installed: the target's state refuses it.
            "PLUGIN_DOWNGRADE_REFUSED" or "PLUGIN_STILL_INSTALLED" => new(409, "Conflict"),
            // Exact row: the package is well formed, but the running host version is outside the range it declares.
            "PLUGIN_HOST_VERSION_OUT_OF_RANGE" => new(409, "Conflict"),
            // Exact rows: the plugin registry is a dependency of the install, not part of the caller's request. Off in this
            // deployment is a state conflict (409); unreachable, a bad reply, a refused or altered package are a gateway
            // failing (502); no answer in time is a gateway timeout (504); the operator's trust policy declining a
            // package is a refusal (403). None of the names carries a token for the generic arms.
            "PLUGIN_REGISTRY_DISABLED" => new(409, "Conflict"),
            "PLUGIN_REGISTRY_UNAVAILABLE" or "PLUGIN_REGISTRY_RESPONSE_INVALID" or "PLUGIN_REGISTRY_PACKAGE_REFUSED"
                or "PLUGIN_REGISTRY_CHECKSUM_MISMATCH" => new(502, "Bad Gateway"),
            "PLUGIN_REGISTRY_TIMED_OUT" => new(504, "Gateway Timeout"),
            "PLUGIN_REGISTRY_NOT_TRUSTED" => new(403, "Forbidden"),
            // Exact rows: the registry wants a licence key, or refused the one sent. The caller can fix it by entering a
            // key, so 422; never 401 or 403, which the admin panel reads as its own sign-in or permission failing.
            "PLUGIN_REGISTRY_LICENCE_REQUIRED" or "PLUGIN_REGISTRY_LICENCE_INVALID" => new(422, "Unprocessable Content"),
            // Exact rows: the version asked for does not run on this host, or the registry withdrew or flagged it. The request
            // is well formed and the version's state refuses it, a conflict (409); none of the names carries a CONFLICT token.
            "PLUGIN_REGISTRY_HOST_INCOMPATIBLE" or "PLUGIN_REGISTRY_VERSION_YANKED" or "PLUGIN_REGISTRY_VERSION_COMPROMISED"
                => new(409, "Conflict"),
            // Exact rows: this installation's identity towards the registry. The address and the registration state are
            // things the operator fixes (422, 409); a refusal of a signed call is the registry declining us (502); the
            // registry's own limit is a 429. None of the names carries a token for the generic arms.
            "PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID" => new(422, "Unprocessable Content"),
            "PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED" or "PLUGIN_REGISTRY_INSTANCE_REVOKED" or "PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED"
                => new(409, "Conflict"),
            "PLUGIN_REGISTRY_INSTANCE_SIGNATURE_REJECTED" or "PLUGIN_REGISTRY_INSTANCE_CLOCK_SKEW" or "PLUGIN_REGISTRY_INSTANCE_REPLAY_REJECTED"
                or "PLUGIN_REGISTRY_DOWNLOAD_TICKET_REFUSED" or "PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE" => new(502, "Bad Gateway"),
            "PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED" => new(429, "Too Many Requests"),
            // Exact row: a plugin's own code failed the call (a gateway between us and code we do not own, 502); the name
            // carries no token.
            "PLUGIN_FAILED" => new(502, "Bad Gateway"),
            // Exact row: a plugin did not answer within the call time limit (a gateway that timed out, 504); the name carries no token.
            "PLUGIN_TIMEOUT" => new(504, "Gateway Timeout"),
            // Exact row: a plugin's store for this account is full. A quota refusal answers 402 like QUOTA_EXCEEDED
            // (controllers.md), and the code needs its own row because that one matches only the exact code.
            "PLUGIN_STORE_QUOTA_EXCEEDED" => new(402, "Payment Required"),
            // Exact row: the user is not on the caller's account, which reads as absent (404), not as a
            // malformed request; the name carries no NOT_FOUND token for the generic arm to catch.
            "CLIENT_USER_NOT_LINKED" => new(404, "Not Found"),
            // Exact row: the name says "permission", not one of the FORBIDDEN / ACCESS_DENIED tokens the
            // generic arms below catch, and a missing grant is a refusal (403), never a malformed request.
            "CLIENT_PERMISSION_DENIED" => new(403, "Forbidden"),
            // Exact row: a suspended or closed account is refused ordering and online payment. The
            // caller is known and the request well-formed; the account's status forbids it, so 403,
            // and neither name carries a FORBIDDEN token for the generic arm.
            "CLIENT_SUSPENDED" or "CLIENT_CLOSED" or "CLIENT_CARD_PAYMENT_DISABLED" or "CLIENT_SSO_DISABLED" => new(403, "Forbidden"),
            // Exact rows: a contact email that another contact already holds, an owner who cannot be
            // removed, and a closed account whose people and contacts are frozen are state conflicts
            // (409), and none of the names carries a CONFLICT / ALREADY token for the generic arms.
            "CONTACT_EMAIL_IN_USE" or "CLIENT_OWNER_CANNOT_BE_REMOVED" or "CLIENT_RECORD_CLOSED"
                => new(409, "Conflict"),
            // Exact row: an unpaid invoice stands in the way, a state conflict, and the name carries no
            // CONFLICT / ALREADY token for the generic arms to catch.
            "RENEWAL_INVOICE_OPEN" or "CLIENT_TAX_EXEMPT" or "CLIENT_CURRENCY_LOCKED_BY_CREDIT" or "CREDIT_TOP_UP_TOO_MANY_OPEN"
                => new(409, "Conflict"),
            // Exact row: the gateway module is off, missing or has no stored credential capability, a state of the
            // deployment rather than a malformed request, and the name carries no CONFLICT / ALREADY token.
            "STORED_CREDENTIALS_NOT_SUPPORTED" => new(409, "Conflict"),
            // Exact row: a stored card charge of the invoice has not finished, a state conflict for cancelling or
            // paying by hand, and the name carries no CONFLICT / ALREADY token.
            "INVOICE_STORED_CHARGE_UNRESOLVED" => new(409, "Conflict"),
            // Exact row: a stored charge of the invoice is held for an operator, the same state conflict.
            "INVOICE_STORED_CHARGE_HELD" => new(409, "Conflict"),
            // Exact rows: no bank transfer waits on the invoice (the second of two confirms lands here), and the admin has bank
            // transfer switched off; both are a state the request cannot change, a conflict, and neither name carries a
            // CONFLICT / ALREADY token for the generic arms.
            "INVOICE_BANK_TRANSFER_NOT_PENDING" or "BANK_TRANSFER_NOT_AVAILABLE" => new(409, "Conflict"),
            // Exact row: the hosted gateway refused or failed the refund, an upstream refusal
            // rather than a malformed request, and neither name carries the _UPSTREAM_FAILED suffix.
            "GATEWAY_REFUND_REFUSED" or "REFUND_ADVICE_UNAVAILABLE" => new(502, "Bad Gateway"),
            // Exact row: a fulfilment plugin's decision stands in the way of the refund, a state
            // conflict, and the name carries no CONFLICT / ALREADY token for the generic arms.
            "REFUND_BLOCKED_BY_PLUGIN" => new(409, "Conflict"),
            // Exact rows: the registrar's own state stands in the way (no such operation there, the
            // domain not active yet, no privacy subscription), and none of the names carries a
            // CONFLICT / ALREADY token for the generic arms.
            "REGISTRAR_OPERATION_UNSUPPORTED" or "REGISTRAR_DOMAIN_NOT_ACTIVE" or "REGISTRAR_WHOIS_PRIVACY_UNAVAILABLE"
                => new(409, "Conflict"),
            // Exact rows: the name is not free to buy (a state conflict, and the name carries no
            // CONFLICT / ALREADY token); the operator's registrar balance is short, which is the
            // deployment's state rather than the caller's (503).
            "REGISTRAR_DOMAIN_UNAVAILABLE" => new(409, "Conflict"),
            "REGISTRAR_INSUFFICIENT_FUNDS" => new(503, "Service Unavailable"),
            // Exact row: the body parsed but moves no money, a meaning that is wrong rather than a
            // malformed request.
            "TRANSACTION_AMOUNT_REQUIRED" => new(422, "Unprocessable Content"),
            // Exact row: an entry of the credit history stands in the way of the edit or delete, a state conflict, and the
            // name carries a LOCKED suffix that the generic arm below would answer 423 for.
            "TRANSACTION_CREDIT_LOCKED" => new(409, "Conflict"),
            // Exact rows: the credit note is in a state that refuses the action, a conflict with what is stored and not a malformed
            // request; neither name carries a CONFLICT token.
            "CREDIT_NOTE_INVALID_STATE" or "CREDIT_NOTE_REFUND_NOT_VOIDABLE" => new(409, "Conflict"),

            // SSH key management: the service is the caller's, but its state or the key's binding
            // does not allow the change, or the hosting server did not confirm it.
            "SSH_KEY_LIMIT_REACHED" or "SSH_KEY_STALE" or "SSH_ACCESS_UNAVAILABLE"
                or "SSH_AGENT_NOT_VERIFIED" or "SSH_AGENT_ENDPOINT_IN_USE" => new(409, "Conflict"),
            "SSH_SERVER_UNAVAILABLE" or "SSH_HOST_KEY_UNREADABLE" or "SSH_AGENT_HOST_UNREACHABLE"
                or "SSH_AGENT_PORT_REFUSED" or "SSH_AGENT_CONNECTION_CLOSED" => new(503, "Service Unavailable"),
            // Exact rows: a server, a server group or an administrator account is still needed by
            // other records or by the installation itself, a state conflict whose names carry no
            // CONFLICT / ALREADY token for the generic arms.
            "SERVER_IN_USE" or "SERVER_GROUP_IN_USE" or "USER_CANNOT_DELETE_SELF" or "USER_LAST_ADMIN"
                => new(409, "Conflict"),
            // Exact rows: a category or client group that still holds sub-categories or records, a state conflict whose
            // names carry no CONFLICT / ALREADY token for the generic arms.
            "KBCATEGORY_IN_USE" or "DOWNLOAD_CATEGORY_IN_USE" or "PREDEFINED_REPLY_CATEGORY_IN_USE" or "CLIENT_GROUP_IN_USE"
                => new(409, "Conflict"),
            // Exact rows: the rate source is down (a dependency, so 503 with Retry-After), or it
            // quotes nothing against the configured base (the deployment's state, a conflict).
            "EXCHANGE_RATE_SOURCE_UNAVAILABLE" => new(503, "Service Unavailable"),
            "EXCHANGE_RATE_BASE_NOT_QUOTED" => new(409, "Conflict"),
            // Exact rows: only a failed fulfilment record may be retried, so the record's state stands
            // in the way (a conflict); a fulfilment plugin that cannot advise on a refund is a
            // dependency that did not answer (503 with Retry-After). Neither name carries a token.
            "FULFILMENT_RECORD_NOT_RETRYABLE" => new(409, "Conflict"),
            // Exact row: only a failed line of an accepted order may be retried, so the line's state stands in the
            // way (a conflict), and the name carries no CONFLICT / ALREADY token for the generic arms.
            "ORDER_LINE_NOT_RETRYABLE" => new(409, "Conflict"),
            // Exact rows: the line changed under the attempt, or is not in the state a refund needs, so the line's
            // state stands in the way (a conflict), and neither name carries a CONFLICT / ALREADY token.
            "ORDER_LINE_CHANGED" or "ORDER_LINE_NOT_REFUNDABLE" or "ORDER_LINE_REFUND_IN_PROGRESS" => new(409, "Conflict"),
            // Exact row: the line has no claim to release (not a domain renewal, already applied, or not
            // claimed), a state conflict whose name carries no token for the generic arms.
            "RENEWAL_CLAIM_NOT_HELD" => new(409, "Conflict"),
            "FULFILMENT_ADVICE_UNAVAILABLE" => new(503, "Service Unavailable"),
            // Exact row: no invoice PDF render slot came free for the request within the configured
            // wait, the server's capacity rather than the request (503 with Retry-After), and the name
            // carries no token for it.
            "INVOICE_PDF_RENDER_BUSY" => new(503, "Service Unavailable"),
            // Exact row: the identity plugin that decides which client a caller acts for failed or timed
            // out, or two are enabled; a dependency that did not answer (503 with Retry-After), and the
            // name carries no token for it.
            "IDENTITY_PLUGIN_UNAVAILABLE" => new(503, "Service Unavailable"),
            var code when code.Contains("UNAUTHORIZED", StringComparison.Ordinal) => new(401, "Unauthorized"),
            var code when code.Contains("FORBIDDEN", StringComparison.Ordinal) => new(403, "Forbidden"),
            var code when code.Contains("ACCESS_DENIED", StringComparison.Ordinal) => new(403, "Forbidden"),
            var code when code.Contains("NOT_FOUND", StringComparison.Ordinal) => new(404, "Not Found"),
            var code when code.Contains("CONFLICT", StringComparison.Ordinal) => new(409, "Conflict"),
            var code when code.Contains("ALREADY", StringComparison.Ordinal) => new(409, "Conflict"),
            var code when code.EndsWith("_EXISTS", StringComparison.Ordinal) => new(409, "Conflict"),
            var code when code.Contains("VALIDATION", StringComparison.Ordinal) => new(422, "Validation Failed"),
            var code when code.Contains("RATE_LIMIT", StringComparison.Ordinal) => new(429, "Too Many Requests"),
            var code when code.EndsWith("_LOCKED", StringComparison.Ordinal) => new(423, "Locked"),
            var code when code.EndsWith("_UPSTREAM_FAILED", StringComparison.Ordinal) => new(502, "Bad Gateway"),
            // hostpanel's three suffix rows: the frontends branch on these codes, so the table
            // maps their suffixes to statuses rather than the codes carrying a status token.
            var code when code.EndsWith("_SEND_FAILED", StringComparison.Ordinal) => new(502, "Bad Gateway"),
            var code when code.EndsWith("_TOO_LARGE", StringComparison.Ordinal) => new(413, "Content Too Large"),
            var code when code.EndsWith("_TOKEN_INVALID", StringComparison.Ordinal) => new(403, "Forbidden"),
            var code when code.Contains("UNEXPECTED", StringComparison.Ordinal) => new(500, "Unexpected Error"),
            var code when code.Contains("SERVICE_UNAVAILABLE", StringComparison.Ordinal)
                => new(503, "Service Unavailable"),
            var code when code.Contains("TIMEOUT", StringComparison.Ordinal) => new(503, "Service Unavailable"),
            var code when code.EndsWith("_NOT_CONFIGURED", StringComparison.Ordinal)
                => new(503, "Service Unavailable"),
            _ => new(400, "Bad Request"),
        };
    }
}
