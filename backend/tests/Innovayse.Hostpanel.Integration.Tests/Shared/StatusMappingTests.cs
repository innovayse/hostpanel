using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;
/// <summary>The one code → status table, pinned row by row so a stray substring cannot move a status.</summary>
public sealed class StatusMappingTests
{
    /// <summary>
    /// Each documented code answers the documented status: the table in <c>rules/controllers.md</c>,
    /// row by row, plus the product-neutral <c>HOSTPANEL_*</c> codes the shared layer emits itself.
    /// </summary>
    /// <param name="code">An error code.</param>
    /// <param name="status">The status the table lists for it.</param>
    [Theory]
    [InlineData(SharedErrorCodes.ValidationError, 400)]
    [InlineData("INVOICE_VALIDATION_FAILED", 422)]
    [InlineData("QUOTA_EXCEEDED", 402)]
    [InlineData(HostpanelErrorCodes.SERVICE_FIRST_INVOICE_UNPAID, 402)]
    [InlineData(HostpanelErrorCodes.PROVISIONING_ACCOUNT_CONFLICT, 409)]
    [InlineData(SharedErrorCodes.Unauthorized, 401)]
    [InlineData(SharedErrorCodes.Forbidden, 403)]
    [InlineData(HostpanelErrorCodes.INVOICE_NOT_FOUND, 404)]
    [InlineData(HostpanelErrorCodes.CLIENT_USER_NOT_LINKED, 404)]
    [InlineData("SKU_ALREADY_EXISTS", 409)]
    [InlineData(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN, 409)]
    [InlineData(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE, 409)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED, 409)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_ACTIVE, 409)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_WHOIS_PRIVACY_UNAVAILABLE, 409)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE, 409)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS, 503)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED, 422)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_FOUND, 404)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED, 503)]
    [InlineData(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED, 502)]
    [InlineData(HostpanelErrorCodes.TRANSACTION_AMOUNT_REQUIRED, 422)]
    [InlineData(HostpanelErrorCodes.SERVER_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.SERVER_GROUP_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.USER_CANNOT_DELETE_SELF, 409)]
    [InlineData(HostpanelErrorCodes.USER_LAST_ADMIN, 409)]
    [InlineData(HostpanelErrorCodes.KBCATEGORY_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.DOWNLOAD_CATEGORY_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.PREDEFINED_REPLY_CATEGORY_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.USER_NOT_FOUND, 404)]
    [InlineData(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE, 503)]
    [InlineData(HostpanelErrorCodes.EXCHANGE_RATE_BASE_NOT_QUOTED, 409)]
    [InlineData(HostpanelErrorCodes.SSH_AGENT_NOT_VERIFIED, 409)]
    [InlineData(HostpanelErrorCodes.SSH_AGENT_ENDPOINT_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.SSH_HOST_KEY_UNREADABLE, 503)]
    [InlineData(HostpanelErrorCodes.SSH_AGENT_HOST_UNREACHABLE, 503)]
    [InlineData(HostpanelErrorCodes.SSH_AGENT_PORT_REFUSED, 503)]
    [InlineData(HostpanelErrorCodes.SSH_AGENT_CONNECTION_CLOSED, 503)]
    [InlineData("PAYLOAD_TOO_LARGE", 413)]
    [InlineData("RATE_LIMITED", 429)]
    [InlineData("ACCOUNT_LOCKED", 423)]
    [InlineData("GATEWAY_UPSTREAM_FAILED", 502)]
    [InlineData("UNEXPECTED", 500)]
    [InlineData(SharedErrorCodes.Unexpected, 500)]
    [InlineData(HostpanelErrorCodes.HOSTPANEL_RATE_LIMITED, 429)]
    [InlineData("SERVICE_UNAVAILABLE", 503)]
    [InlineData("GATEWAY_TIMEOUT", 503)]
    [InlineData("MAILER_NOT_CONFIGURED", 503)]
    [InlineData("VALIDATION_NOT_CONFIGURED", 422)]
    [InlineData("SHARE_NOT_FOUND_UPSTREAM_FAILED", 404)]
    [InlineData(HostpanelErrorCodes.CONTACT_SEND_FAILED, 502)]
    [InlineData(HostpanelErrorCodes.BRANDING_IMAGE_TOO_LARGE, 413)]
    [InlineData(HostpanelErrorCodes.SETUP_TOKEN_INVALID, 403)]
    [InlineData(SharedErrorCodes.DemoActionDisabled, 403)]
    [InlineData(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED, 403)]
    [InlineData(HostpanelErrorCodes.CLIENT_SUSPENDED, 403)]
    [InlineData(HostpanelErrorCodes.CLIENT_CLOSED, 403)]
    [InlineData(HostpanelErrorCodes.CONTACT_EMAIL_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.CLIENT_OWNER_CANNOT_BE_REMOVED, 409)]
    [InlineData(HostpanelErrorCodes.CLIENT_RECORD_CLOSED, 409)]
    [InlineData(HostpanelErrorCodes.EMAIL_ALREADY_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.PRODUCT_SKU_ALREADY_IN_USE, 409)]
    [InlineData(HostpanelErrorCodes.EXTERNAL_ACCOUNT_ALREADY_LINKED, 409)]
    [InlineData(HostpanelErrorCodes.FULFILMENT_RECORD_NOT_FOUND, 404)]
    [InlineData(HostpanelErrorCodes.FULFILMENT_RECORD_NOT_RETRYABLE, 409)]
    [InlineData(HostpanelErrorCodes.FULFILMENT_ADVICE_UNAVAILABLE, 503)]
    [InlineData("SHARE_NOT_FOUND_SEND_FAILED", 404)]
    [InlineData("SOMETHING_ELSE", 400)]
    public void MapsCodeToStatus(string code, int status)
    {
        StatusMapping.For(code).Status.Should().Be(status);
    }
}
