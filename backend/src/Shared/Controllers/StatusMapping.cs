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
            // Exact row: the order number sequence outgrew the configured number length, a state of
            // the deployment rather than of the request, and the name carries no token for it.
            "ORDER_NUMBER_RANGE_EXHAUSTED" => new(503, "Service Unavailable"),
            // Exact rows: the stored service or product is not ready to provision, a state conflict
            // rather than a malformed request, and neither name carries a CONFLICT token.
            "SERVICE_HOSTING_DETAILS_MISSING" or "PRODUCT_PACKAGE_MISSING" => new(409, "Conflict"),
            // Exact row: only a terminated service has a termination date to correct, so the stored
            // status stands in the way (a state conflict), and the name carries no CONFLICT token.
            "SERVICE_NOT_TERMINATED" => new(409, "Conflict"),
            // A plugin shipped with the build: the request is well-formed, the target's state refuses it.
            "PLUGIN_BUILT_IN" => new(409, "Conflict"),
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
            "RENEWAL_INVOICE_OPEN" or "CLIENT_TAX_EXEMPT" => new(409, "Conflict"),
            // Exact row: the deployment has no card gateway, a state of the system rather than a
            // malformed request, and the name carries no CONFLICT / ALREADY token for the generic arms.
            "PAYMENT_METHOD_UNAVAILABLE" => new(409, "Conflict"),
            // Exact row: the card gateway or the hosted gateway refused or failed the refund, an
            // upstream refusal rather than a malformed request, and neither name carries the
            // _UPSTREAM_FAILED suffix.
            "CARD_REFUND_REFUSED" or "GATEWAY_REFUND_REFUSED" => new(502, "Bad Gateway"),
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
            // Exact rows: a category that still holds sub-categories or records, a state conflict whose
            // names carry no CONFLICT / ALREADY token for the generic arms.
            "KBCATEGORY_IN_USE" or "DOWNLOAD_CATEGORY_IN_USE" or "PREDEFINED_REPLY_CATEGORY_IN_USE"
                => new(409, "Conflict"),
            // Exact rows: the rate source is down (a dependency, so 503 with Retry-After), or it
            // quotes nothing against the configured base (the deployment's state, a conflict).
            "EXCHANGE_RATE_SOURCE_UNAVAILABLE" => new(503, "Service Unavailable"),
            "EXCHANGE_RATE_BASE_NOT_QUOTED" => new(409, "Conflict"),
            // Exact rows: only a failed fulfilment record may be retried, so the record's state stands
            // in the way (a conflict); a fulfilment plugin that cannot advise on a refund is a
            // dependency that did not answer (503 with Retry-After). Neither name carries a token.
            "FULFILMENT_RECORD_NOT_RETRYABLE" => new(409, "Conflict"),
            "FULFILMENT_ADVICE_UNAVAILABLE" => new(503, "Service Unavailable"),
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
