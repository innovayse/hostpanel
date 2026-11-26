using FluentValidation;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Clients.Commands.AcceptInvitation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Support.Common;
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

        // Resolves which clients a campaign's segments reach, for the audience count and for fixing the recipients.
        services.AddScoped<CampaignAudience>();

        // How many campaign mails the send rate still allows, read by the sender before every batch.
        services.AddScoped<CampaignSendThrottle>();

        // Asks the enabled fulfilment plugins to cap a refund before any money moves.
        services.AddScoped<RefundAdviser>();
        services.AddScoped<GatewayPaymentCanceller>();

        // Finds or creates a client's customer at a stored credential gateway, and binds a stored method to its consent: each is
        // shared by the save session and by the payment that saves its method.
        services.AddScoped<GatewayCustomerEnsurer>();
        services.AddScoped<StoredMethodBinder>();
        services.AddScoped<UnboundConsentRevoker>();

        // The connection state of plugin integrations (IPluginStatus), cached and worded for the Integrations page.
        services.AddScoped<PluginConnectionReader>();

        // The part of a plugin call the client and admin areas share: route matching, body cap, audit, reply mapping.
        services.AddScoped<PluginCallRunner>();
        services.AddScoped<PluginCallGate>();

        // Lists the widgets enabled plugins put into a page slot, for the client and the admin slot index.
        services.AddScoped<PluginSlotIndexBuilder>();

        // The origins a plugin view may frame or link to for the caller, shared by the page reader and the slot widget view.
        services.AddScoped<PluginFrameOriginResolver>();

        // The slot, caller and context checks the client slot index and the slot widget view share.
        services.AddScoped<ClientPluginSlotGate>();

        // The slot and context checks the admin slot index and the admin slot widget view share.
        services.AddScoped<AdminPluginSlotGate>();

        // Reads the view of a widget the slot index lists, shared by the client and the admin slot widget views.
        services.AddScoped<PluginSlotWidgetViewReader>();

        // The one path a key reaches a hosting account by and the one it leaves by, shared by the client's own SSH use
        // cases and the plugin SSH access.
        services.AddScoped<SshKeyInstaller>();
        services.AddScoped<SshKeyRevocation>();

        // Gives a new knowledge base article or category a slug nobody else has.
        services.AddScoped<KbSlugAllocator>();
        services.AddScoped<KbSlugSaver>();

        // The one gate every plugin host access to a client's hosting passes (#1294).
        services.AddScoped<PluginHostingGate>();

        // The SSH half of the plugin host API: issues and revokes a plugin's identities for the call's client (#758).
        services.AddScoped<PluginSshAccessService>();

        // The websites and databases half of the plugin host API: reads the hosting capabilities and sets up and removes a
        // plugin's sites and databases for the call's client (#1294).
        services.AddScoped<PluginHostingService>();

        // The service connection half of the plugin host API: the server and account of one of the call's client's
        // services (#1357).
        services.AddScoped<PluginServiceConnectionService>();

        // The payment half of the plugin host API: a payment plugin's callback asks the host to read one gateway order (#1356).
        services.AddScoped<PluginOrderReadService>();

        // Which plans a service can move to and what each move costs, shared by the client's option list and the request itself.
        services.AddScoped<ServicePlanChangeQuoter>();
        services.AddScoped<ServicePlanChangeSettingsReader>();
        services.AddScoped<SettingSource>();
        services.AddScoped<ExchangeRateSettingsReader>();

        // The rate a new invoice is raised at: the one answer every invoice-creating use case asks.
        services.AddScoped<ClientTaxRateResolver>();
        // The operator's fraud thresholds and limits, read by the fraud check and by the settings screen.
        services.AddScoped<FraudSettingsReader>();

        // This installation's identity towards the plugin registry: where it is kept, and the enrolment that registers and verifies it.
        services.AddScoped<RegistryInstanceStore>();
        services.AddScoped<RegistryInstanceEnrollment>();
        // The update a registry row offers for an installed plugin: only a version that runs on this host.
        services.AddScoped<RegistryPluginReader>();
        services.AddScoped<PluginUpdateOffer>();
        // The install record both plugin install use cases (upload and registry) write for an accepted package.
        services.AddScoped<InstalledPluginRecorder>();
        // The answer of an install or a removal: applied in the running process when the plugin can be reloaded, else a restart.
        services.AddScoped<PluginChangeApplier>();

        return services;
    }
}
