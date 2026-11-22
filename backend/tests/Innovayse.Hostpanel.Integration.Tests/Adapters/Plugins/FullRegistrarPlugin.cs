using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.Results;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// A registrar plugin that implements the operations contract and the three optional contracts the host calls (DNS management,
/// email forwarding and WHOIS), scripted like <see cref="ScriptedRegistrarPlugin"/>.
/// </summary>
public sealed class FullRegistrarPlugin : ScriptedRegistrarPlugin, IRegistrarDnsManagementPlugin, IRegistrarEmailForwardingPlugin, IRegistrarWhoisPlugin
{
    /// <summary>Initializes a new instance of the <see cref="FullRegistrarPlugin"/> class.</summary>
    /// <param name="configuration">The configuration composed for this plugin alone.</param>
    public FullRegistrarPlugin(IConfiguration configuration)
        : base(configuration)
    {
    }

    /// <summary>Gets or sets the answer of the WHOIS lookup.</summary>
    public ValueResult<RegistrarWhois, RegistrarRefusalReason> Whois { get; set; } =
        ValueResult<RegistrarWhois, RegistrarRefusalReason>.Done(
            new RegistrarWhois("Test Registrar", "Test Registrant", OrderExpiry.AddYears(-1), OrderExpiry));

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> SetDnsManagementAsync(RegistrarFlagContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> SetEmailForwardingAsync(RegistrarFlagContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> AddEmailForwardingRuleAsync(RegistrarEmailForwardingRuleContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> UpdateEmailForwardingRuleAsync(RegistrarEmailForwardingRuleContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> DeleteEmailForwardingRuleAsync(RegistrarEmailForwardingRemovalContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarWhois, RegistrarRefusalReason>> GetWhoisAsync(RegistrarWhoisContext context, CancellationToken ct)
    {
        return Answer(context, Whois);
    }
}
