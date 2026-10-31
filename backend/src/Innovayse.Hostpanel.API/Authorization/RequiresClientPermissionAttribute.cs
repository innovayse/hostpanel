using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// Admits a client portal action only to a user who holds the named <see cref="ClientPermission"/>
/// on a client account. The one place the permissions of a linked user are enforced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every client portal action</b> (an action behind <c>Policies.Client</c>) carries exactly one of
/// two declarations: this attribute, or <see cref="AvailableToEveryClientUserAttribute"/> when any user
/// of the account may call it. <c>ClientPortalPermissionTests</c> fails the build for an action with
/// neither, so a route added later cannot skip the check by forgetting it. Several flags in one
/// attribute mean the caller must hold every one of them.
/// </para>
/// <para>
/// <b>Identity comes from the credential.</b> The filter asks <see cref="IClientAccess"/>, which
/// resolves the caller through <c>ICurrentUserService</c>; no route value, query string or body can
/// name whose permissions apply. The account owner holds every permission. It fails closed: no
/// owned record and no stored link, a link that does not grant the flag, or a flag this product
/// never defined all answer 403 <c>CLIENT_PERMISSION_DENIED</c> in the standard error body.
/// </para>
/// <para>
/// <b>The mapping</b> (the declarations on the actions are the source of truth; this table is the
/// reading guide):
/// </para>
/// <list type="table">
/// <listheader><term>Permission</term><description>Client portal actions</description></listheader>
/// <item><term>(every account user)</term><description><c>GET api/clients/me</c> (the profile the portal opens with)</description></item>
/// <item><term><see cref="ClientPermission.ModifyMasterProfile"/></term><description><c>PUT api/clients/me</c></description></item>
/// <item><term><see cref="ClientPermission.ViewManageContacts"/></term><description>contacts: <c>GET, POST api/clients/me/contacts</c>, <c>PUT, DELETE api/clients/me/contacts/{id}</c></description></item>
/// <item><term><see cref="ClientPermission.ViewPayInvoices"/></term><description>invoices: every <c>api/me/invoices</c> action (list, read, pay, gateway payment start and complete); every <c>api/me/payment-methods</c> action; <c>GET api/me/services/{id}/invoices</c> (together with <see cref="ClientPermission.ViewProductsServices"/>)</description></item>
/// <item><term><see cref="ClientPermission.ViewAcceptQuotes"/></term><description>none yet: the portal has no quote endpoint; the flag is reserved for when it gets one</description></item>
/// <item><term><see cref="ClientPermission.ViewOpenSupportTickets"/></term><description>tickets: every <c>api/me/tickets</c> action (list, read, open, reply)</description></item>
/// <item><term><see cref="ClientPermission.ViewProductsServices"/></term><description><c>GET api/me/services</c>, <c>GET api/me/services/{id}/cancellation-status</c>, <c>GET api/me/services/{id}/hosting-info</c>, <c>GET api/me/services/{id}/ssh</c>, <c>GET api/me/services/{id}/ssh/keys</c></description></item>
/// <item><term><see cref="ClientPermission.ViewModifyPasswords"/></term><description><c>POST api/me/services/{id}/change-password</c>, <c>POST api/me/services/{id}/ssh/keys</c>, <c>DELETE api/me/services/{id}/ssh/keys/{keyId}</c></description></item>
/// <item><term><see cref="ClientPermission.AllowSingleSignOn"/></term><description><c>GET api/me/services/{id}/cpanel-sso</c></description></item>
/// <item><term><see cref="ClientPermission.ViewDomains"/></term><description>domains, read: <c>GET api/me/domains</c>, <c>GET api/me/domains/{id}</c>, <c>/nameservers</c>, <c>/whois</c>, <c>/contact</c></description></item>
/// <item><term><see cref="ClientPermission.ManageDomainSettings"/></term><description>domains, change: auto-renew, WHOIS privacy, registrar lock, nameservers, WHOIS contact, EPP code, DNS records, DNS management, email forwarding (toggle and rules)</description></item>
/// <item><term><see cref="ClientPermission.PlaceNewOrders"/></term><description>orders: <c>POST api/me/services</c>, <c>POST api/me/services/{id}/cancel</c>, <c>POST api/me/services/{id}/setup</c>, <c>POST api/me/domains/transfer-order</c>, <c>POST api/me/domains/{id}/renew</c></description></item>
/// <item><term><see cref="ClientPermission.ViewManageAffiliate"/></term><description>none yet: the portal has no affiliate endpoint; the flag is reserved for when it gets one</description></item>
/// <item><term><see cref="ClientPermission.ViewEmails"/></term><description>emails: both <c>api/me/emails</c> actions</description></item>
/// <item><term><see cref="ClientPermission.All"/></term><description>the users of the account: <c>GET, POST api/me/users</c>, <c>DELETE api/me/users/{id}</c> (the flags have no user-management bit, so this takes full access, which the owner always has)</description></item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresClientPermissionAttribute : Attribute, IAsyncAuthorizationFilter, IApiResponseMetadataProvider
{
    /// <summary>Creates the declaration.</summary>
    /// <param name="permission">
    /// The permission the action needs; several flags mean all of them. Must name at least one flag
    /// and only flags <see cref="ClientPermission"/> defines.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown for <see cref="ClientPermission.None"/> (use <see cref="AvailableToEveryClientUserAttribute"/>
    /// for an action that needs nothing) or a value with a bit nobody defined; a mistaken declaration
    /// fails when the controller is first read, not by silently admitting everyone.
    /// </exception>
    public RequiresClientPermissionAttribute(ClientPermission permission)
    {
        if (permission == ClientPermission.None || (permission & ~ClientPermission.All) != ClientPermission.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permission), permission, "A client permission must name at least one defined flag.");
        }

        Permission = permission;
    }

    /// <summary>The permission the action needs; the caller must hold every flag in it.</summary>
    public ClientPermission Permission { get; }

    /// <inheritdoc/>
    public Type? Type => typeof(ErrorResponse);

    /// <inheritdoc/>
    public int StatusCode => StatusCodes.Status403Forbidden;

    /// <inheritdoc/>
    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        ArgumentNullException.ThrowIfNull(contentTypes);

        // The refusal is the product's one error body, application/problem+json.
        contentTypes.Clear();
        contentTypes.Add(new MediaTypeHeaderValue(ErrorBody.ContentType));
    }

    /// <summary>Refuses the request unless the caller holds <see cref="Permission"/> on a client account.</summary>
    /// <param name="context">The request being authorized.</param>
    /// <returns>A task that completes once the decision is made.</returns>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.HttpContext.RequestServices;
        var clientAccess = services.GetRequiredService<IClientAccess>();
        var ct = context.HttpContext.RequestAborted;

        // The plugin that decides the account did not answer: say so (503, retryable) rather than a permission refusal.
        if (await clientAccess.IsIdentityUnavailableAsync(ct))
        {
            var unavailable = ErrorBody.ForCode(services, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
            RetryAfterHeader.ApplyFor(context.HttpContext, unavailable.Status);
            context.Result = ErrorBody.ToActionResult(unavailable);
            return;
        }

        var held = await clientAccess.GetCallerPermissionsAsync(ct);

        if ((held & Permission) == Permission)
        {
            return;
        }

        context.Result = ErrorBody.ToActionResult(ErrorBody.ForCode(services, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED));
    }
}
