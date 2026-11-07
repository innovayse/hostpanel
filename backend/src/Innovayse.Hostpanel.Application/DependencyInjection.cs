using FluentValidation;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Commands.AcceptInvitation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Application;

/// <summary>
/// Registers what the Application layer owns: every <see cref="AbstractValidator{T}"/> the
/// commands and queries in this assembly declare.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Adds the Application layer.</summary>
    /// <param name="services">The container.</param>
    /// <returns>The container, for chaining.</returns>
    /// <remarks>
    /// Every <c>AbstractValidator&lt;T&gt;</c> in this assembly, which is where all of them
    /// live: there is not one in the API, the Domain or the Infrastructure project. Registered
    /// Scoped, which is this call's default and is load-bearing rather than incidental:
    /// <c>PlaceOrderValidator</c> takes <c>ICurrentUserService</c>, itself Scoped, so a Singleton
    /// registration here would be a captive dependency that answers every checkout with the first
    /// caller's identity.
    /// <para>
    /// <c>AddValidatorsFromAssemblyContaining</c>, not a hand-written list: a validator that is
    /// added and not registered is exactly the dead file this whole change exists to stop
    /// producing.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AcceptInvitationCommand>();

        // Runs the passes of the recurring jobs under their lease; scoped because it holds the scoped lease port.
        services.AddScoped<RecurringJobs>();

        // One pass of the fulfilment dispatch, run by the scheduled chain and by the pass kicked after a payment.
        services.AddScoped<FulfilmentDispatcher>();

        // Asks the enabled fulfilment plugins to cap a refund before any money moves.
        services.AddScoped<RefundAdviser>();
        services.AddScoped<GatewayPaymentCanceller>();

        // The connection state of plugin integrations (IPluginStatus), cached and worded for the Integrations page.
        services.AddScoped<PluginConnectionReader>();

        // The part of a plugin call the client and admin areas share: route matching, body cap, audit, reply mapping.
        services.AddScoped<PluginCallRunner>();
        services.AddScoped<PluginCallGate>();

        // Lists the widgets enabled plugins put into a page slot, for the client and the admin slot index.
        services.AddScoped<PluginSlotIndexBuilder>();

        // The slot, caller and context checks the client slot index and the slot widget view share.
        services.AddScoped<ClientPluginSlotGate>();

        // The one path a key reaches a hosting account by and the one it leaves by, shared by the client's own SSH use
        // cases and the plugin SSH access.
        services.AddScoped<SshKeyInstaller>();
        services.AddScoped<SshKeyRevocation>();

        // The SSH half of the plugin host API: issues and revokes a plugin's identities for the call's client (#758).
        services.AddScoped<PluginSshAccessService>();

        return services;
    }
}
