using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.Results;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// A registrar plugin that implements only the operations contract, so a test can show what the host does with a plugin that
/// offers no optional capability. Every answer is scripted by a property and every context it received is kept in order.
/// </summary>
public class ScriptedRegistrarPlugin : IRegistrarOperationsPlugin
{
    /// <summary>The moment the scripted orders report as the expiry.</summary>
    public static readonly DateTimeOffset OrderExpiry = new(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);

    /// <summary>Initializes a new instance of the <see cref="ScriptedRegistrarPlugin"/> class; the activator hands over the plugin's own configuration.</summary>
    /// <param name="configuration">The configuration composed for this plugin alone.</param>
    public ScriptedRegistrarPlugin(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    /// <summary>Gets the configuration the plugin was built with.</summary>
    public IConfiguration Configuration { get; }

    /// <summary>Gets every context the plugin received, in call order.</summary>
    public List<object> Received { get; } = [];

    /// <summary>Gets or sets the answer of every operation that returns no value.</summary>
    public OperationResult<RegistrarRefusalReason> Operation { get; set; } = OperationResult<RegistrarRefusalReason>.Done();

    /// <summary>Gets or sets the answer of register, transfer, renew and the active check.</summary>
    public ValueResult<RegistrarOrder, RegistrarRefusalReason> Order { get; set; } =
        ValueResult<RegistrarOrder, RegistrarRefusalReason>.Done(new RegistrarOrder("REF-1", OrderExpiry, false));

    /// <summary>Gets or sets the answer of the availability check.</summary>
    public ValueResult<RegistrarAvailability, RegistrarRefusalReason> Availability { get; set; } =
        ValueResult<RegistrarAvailability, RegistrarRefusalReason>.Done(new RegistrarAvailability(true));

    /// <summary>Gets or sets the answer of the EPP code read.</summary>
    public ValueResult<RegistrarEppCode, RegistrarRefusalReason> Epp { get; set; } =
        ValueResult<RegistrarEppCode, RegistrarRefusalReason>.Done(new RegistrarEppCode("EPP-CODE"));

    /// <summary>Gets or sets the answer of the contact read.</summary>
    public ValueResult<RegistrarContact, RegistrarRefusalReason> Contact { get; set; } =
        ValueResult<RegistrarContact, RegistrarRefusalReason>.Done(
            new RegistrarContact("Ada", "Lovelace", null, "ada@example.test", "+1555", "1 Main St", null, "Town", "ST", "00100", "US"));

    /// <summary>Gets or sets the answer of the DNS zone read.</summary>
    public ValueResult<IReadOnlyList<RegistrarDnsRecord>, RegistrarRefusalReason> DnsRecords { get; set; } =
        ValueResult<IReadOnlyList<RegistrarDnsRecord>, RegistrarRefusalReason>.Done(
            [new RegistrarDnsRecord("1", RegistrarDnsRecordType.A, "@", "1.2.3.4", 300, null)]);

    /// <summary>
    /// Gets or sets the answer of the price read; when <see langword="null"/> the plugin answers one entry that echoes the
    /// connection it received (account name as the TLD, API key as the currency), so a test can see what the host passed.
    /// </summary>
    public ValueResult<IReadOnlyList<RegistrarTldPricing>, RegistrarRefusalReason>? Pricing { get; set; }

    /// <summary>Gets or sets the answer of the connection test.</summary>
    public PanelConnectionTestResult ConnectionTest { get; set; } = PanelConnectionTestResult.Connected(null, null);

    /// <summary>Gets or sets what the plugin throws from every operation; nothing is thrown when <see langword="null"/>.</summary>
    public Exception? Throws { get; set; }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarAvailability, RegistrarRefusalReason>> CheckAvailabilityAsync(RegistrarAvailabilityContext context, CancellationToken ct)
    {
        return Answer(context, Availability);
    }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarOrder, RegistrarRefusalReason>> RegisterAsync(RegistrarRegisterContext context, CancellationToken ct)
    {
        return Answer(context, Order);
    }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarOrder, RegistrarRefusalReason>> TransferAsync(RegistrarTransferContext context, CancellationToken ct)
    {
        return Answer(context, Order);
    }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarOrder, RegistrarRefusalReason>> RenewAsync(RegistrarRenewContext context, CancellationToken ct)
    {
        return Answer(context, Order);
    }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarOrder, RegistrarRefusalReason>> CheckDomainActiveAsync(RegistrarDomainContext context, CancellationToken ct)
    {
        return Answer(context, Order);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> CancelTransferAsync(RegistrarDomainContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> InitiateOutgoingTransferAsync(RegistrarDomainContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarEppCode, RegistrarRefusalReason>> GetEppCodeAsync(RegistrarDomainContext context, CancellationToken ct)
    {
        return Answer(context, Epp);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> SetNameserversAsync(RegistrarNameserversContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> SetAutoRenewAsync(RegistrarFlagContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> SetWhoisPrivacyAsync(RegistrarFlagContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> SetRegistrarLockAsync(RegistrarFlagContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<ValueResult<RegistrarContact, RegistrarRefusalReason>> GetContactAsync(RegistrarDomainContext context, CancellationToken ct)
    {
        return Answer(context, Contact);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> SetContactAsync(RegistrarContactContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<ValueResult<IReadOnlyList<RegistrarDnsRecord>, RegistrarRefusalReason>> GetDnsRecordsAsync(RegistrarDomainContext context, CancellationToken ct)
    {
        return Answer(context, DnsRecords);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> AddDnsRecordAsync(RegistrarDnsRecordContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> UpdateDnsRecordAsync(RegistrarDnsRecordUpdateContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<OperationResult<RegistrarRefusalReason>> DeleteDnsRecordAsync(RegistrarDnsRecordRemovalContext context, CancellationToken ct)
    {
        return Answer(context, Operation);
    }

    /// <inheritdoc />
    public Task<ValueResult<IReadOnlyList<RegistrarTldPricing>, RegistrarRefusalReason>> GetTldPricingAsync(RegistrarPricingContext context, CancellationToken ct)
    {
        IReadOnlyList<RegistrarTldPricing> echo =
        [
            new RegistrarTldPricing(
                context.Connection.AccountName ?? "none",
                context.Connection.ApiKey ?? "none",
                new Dictionary<int, decimal> { [1] = 1m },
                new Dictionary<int, decimal> { [1] = 2m },
                new Dictionary<int, decimal> { [1] = 3m }),
        ];

        return Answer(context, Pricing ?? ValueResult<IReadOnlyList<RegistrarTldPricing>, RegistrarRefusalReason>.Done(echo));
    }

    /// <inheritdoc />
    public Task<PanelConnectionTestResult> TestConnectionAsync(RegistrarTestConnectionContext context, CancellationToken ct)
    {
        return Answer(context, ConnectionTest);
    }

    /// <summary>Keeps a context in the received list, throws when scripted to, and otherwise hands back the scripted answer.</summary>
    /// <typeparam name="TAnswer">The type of the answer.</typeparam>
    /// <param name="context">The context the host passed.</param>
    /// <param name="answer">The scripted answer.</param>
    /// <returns>A completed task carrying the scripted answer.</returns>
    protected Task<TAnswer> Answer<TAnswer>(object context, TAnswer answer)
    {
        Received.Add(context);
        if (Throws is not null)
        {
            throw Throws;
        }

        return Task.FromResult(answer);
    }
}
