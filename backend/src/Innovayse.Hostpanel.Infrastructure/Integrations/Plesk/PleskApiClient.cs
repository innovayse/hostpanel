using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;

/// <summary>
/// Client for the Plesk XML API (<c>/enterprise/control/agent.php</c>), one server per call.
/// </summary>
/// <remarks>
/// <para>
/// Every operation answers a <see cref="PleskReply"/> or a <see cref="PleskLookup{T}"/> instead of
/// throwing, so the provider decides what a refusal means. Plesk's own error text is never read
/// into a reply or a log line: it can carry a customer's login or domain, and the error code is
/// enough to act on.
/// </para>
/// <para>
/// A transport failure, an error page or a body that is not a Plesk reply is
/// <see cref="PleskReply.Unreadable"/>, and a lookup that meets one is
/// <see cref="Models.Enums.PleskLookupOutcome.Unknown"/>, never "not found", so a failed read can
/// never convince a caller that an account is gone.
/// </para>
/// </remarks>
public sealed class PleskApiClient
{
    /// <summary>The path of the XML API on a Plesk server.</summary>
    private const string AgentPath = "/enterprise/control/agent.php";

    /// <summary>The header the secret API key travels in.</summary>
    private const string KeyHeader = "KEY";

    /// <summary>The media type Plesk expects a packet in.</summary>
    private const string XmlMediaType = "text/xml";

    /// <summary>Customer status Plesk uses for an active account.</summary>
    private const int ActiveStatus = 0;

    /// <summary>Customer status Plesk uses for an account disabled by the administrator.</summary>
    private const int DisabledByAdminStatus = 16;

    /// <summary>The address type Plesk gives an IP every subscription may share.</summary>
    private const string SharedIpType = "shared";

    /// <summary>The hosting type of a subscription with a web space.</summary>
    private const string VirtualHostingType = "vrt_hst";

    /// <summary>The HTTP client, resolved by name so the Plesk timeout and resilience profile apply.</summary>
    private readonly HttpClient _http;

    /// <summary>Records failed calls without the panel's own text.</summary>
    private readonly ILogger<PleskApiClient> _logger;

    /// <summary>Initializes a new instance of the <see cref="PleskApiClient"/> class.</summary>
    /// <param name="http">The HTTP client, resolved by name so the Plesk timeout and resilience profile apply.</param>
    /// <param name="logger">Records failed calls without the panel's own text.</param>
    public PleskApiClient(HttpClient http, ILogger<PleskApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>Reads the server's version and customer count, which also proves the key works.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The server's details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the server refuses or cannot be read.</exception>
    public async Task<PleskServerInfo> GetServerInfoAsync(PleskConnection connection, CancellationToken ct)
    {
        var reply = await SendAsync(connection, new XElement("server", new XElement("get", new XElement("stat"))), ct);
        if (reply.Result?.Element("stat") is not { } stat)
        {
            throw new InvalidOperationException($"Plesk server info failed (code {reply.ErrorCode?.ToString(CultureInfo.InvariantCulture) ?? "none"}).");
        }

        var count = int.TryParse(stat.Element("objects")?.Element("clients")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clients)
            ? clients
            : 0;
        return new PleskServerInfo(count, stat.Element("version")?.Element("plesk_version")?.Value);
    }

    /// <summary>Looks a customer up by login.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="login">The customer's login.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The customer, a positive "not found", or "unknown".</returns>
    public async Task<PleskLookup<PleskCustomer>> FindCustomerAsync(PleskConnection connection, string login, CancellationToken ct)
    {
        var reply = await SendAsync(connection, Customer("get", ByLogin(login), new XElement("dataset", new XElement("gen_info"))), ct);
        if (!reply.IsSuccess)
        {
            return reply.ErrorCode == PleskErrorCodes.ObjectNotFound ? PleskLookup<PleskCustomer>.NotFound : PleskLookup<PleskCustomer>.Unknown;
        }

        var info = reply.Result?.Element("data")?.Element("gen_info");
        if (info?.Element("login")?.Value is not { } found
            || !int.TryParse(info.Element("status")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
        {
            return PleskLookup<PleskCustomer>.Unknown;
        }

        return PleskLookup<PleskCustomer>.Found(new PleskCustomer(found, status != ActiveStatus));
    }

    /// <summary>Creates a customer whose login and display name are the service's username.</summary>
    /// <param name="connection">The server to create it on.</param>
    /// <param name="login">The customer's login.</param>
    /// <param name="password">The customer's panel password; never logged.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<PleskReply> AddCustomerAsync(PleskConnection connection, string login, string password, CancellationToken ct) =>
        SendAsync(
            connection,
            Customer("add", new XElement("gen_info",
                new XElement("pname", login),
                new XElement("login", login),
                new XElement("passwd", password))),
            ct);

    /// <summary>Disables or re-enables a customer, which disables or re-enables its subscriptions too.</summary>
    /// <param name="connection">The server the customer is on.</param>
    /// <param name="login">The customer's login.</param>
    /// <param name="suspended">Whether the customer is to be disabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply; Plesk answers ok when the customer is already in that state.</returns>
    public Task<PleskReply> SetCustomerSuspendedAsync(PleskConnection connection, string login, bool suspended, CancellationToken ct) =>
        SetCustomerAsync(connection, login, new XElement("status", suspended ? DisabledByAdminStatus : ActiveStatus), ct);

    /// <summary>Changes a customer's panel password.</summary>
    /// <param name="connection">The server the customer is on.</param>
    /// <param name="login">The customer's login.</param>
    /// <param name="password">The new password; never logged.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<PleskReply> SetCustomerPasswordAsync(PleskConnection connection, string login, string password, CancellationToken ct) =>
        SetCustomerAsync(connection, login, new XElement("passwd", password), ct);

    /// <summary>Removes a customer together with every subscription it owns.</summary>
    /// <param name="connection">The server the customer is on.</param>
    /// <param name="login">The customer's login.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<PleskReply> DeleteCustomerAsync(PleskConnection connection, string login, CancellationToken ct) =>
        SendAsync(connection, Customer("del", ByLogin(login)), ct);

    /// <summary>Looks up the subscription a customer owns.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="ownerLogin">The customer's login.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The customer's first subscription; "not found" when the customer has none or does not exist;
    /// "unknown" when the reply could not be used.
    /// </returns>
    public async Task<PleskLookup<PleskSubscription>> FindSubscriptionAsync(PleskConnection connection, string ownerLogin, CancellationToken ct)
    {
        var operation = new XElement("webspace", new XElement("get",
            new XElement("filter", new XElement("owner-login", ownerLogin)),
            new XElement("dataset", new XElement("gen_info"))));
        var reply = await SendAsync(connection, operation, ct);
        if (!reply.IsSuccess)
        {
            return reply.ErrorCode is PleskErrorCodes.ObjectNotFound or PleskErrorCodes.OwnerNotFound
                ? PleskLookup<PleskSubscription>.NotFound
                : PleskLookup<PleskSubscription>.Unknown;
        }

        // A customer with no subscription is answered "ok" with no id at all.
        if (reply.Result is not { } result)
        {
            return PleskLookup<PleskSubscription>.Unknown;
        }

        if (result.Element("id")?.Value is not { } idText)
        {
            return PleskLookup<PleskSubscription>.NotFound;
        }

        return int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && result.Element("data")?.Element("gen_info")?.Element("name")?.Value is { } domain
            ? PleskLookup<PleskSubscription>.Found(new PleskSubscription(id, domain))
            : PleskLookup<PleskSubscription>.Unknown;
    }

    /// <summary>Creates a subscription with a web space on the given service plan.</summary>
    /// <param name="connection">The server to create it on.</param>
    /// <param name="subscription">What the subscription is made of.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<PleskReply> AddSubscriptionAsync(PleskConnection connection, PleskNewSubscription subscription, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var operation = new XElement("webspace", new XElement("add",
            new XElement("gen_setup",
                new XElement("name", subscription.Domain),
                new XElement("owner-login", subscription.OwnerLogin),
                new XElement("htype", VirtualHostingType),
                new XElement("ip_address", subscription.IpAddress)),
            new XElement("hosting", new XElement(VirtualHostingType,
                Property("ftp_login", subscription.SystemUser),
                Property("ftp_password", subscription.Password),
                new XElement("ip_address", subscription.IpAddress))),
            new XElement("plan-name", subscription.PlanName)));
        return SendAsync(connection, operation, ct);
    }

    /// <summary>Looks up a service plan's guid by its name.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="planName">The plan's name, as the product's package names it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plan's guid, a positive "not found", or "unknown".</returns>
    public async Task<PleskLookup<string>> FindPlanGuidAsync(PleskConnection connection, string planName, CancellationToken ct)
    {
        var operation = new XElement("service-plan", new XElement("get",
            new XElement("filter", new XElement("name", planName))));
        var reply = await SendAsync(connection, operation, ct);
        if (!reply.IsSuccess)
        {
            return reply.ErrorCode == PleskErrorCodes.ObjectNotFound ? PleskLookup<string>.NotFound : PleskLookup<string>.Unknown;
        }

        return reply.Result?.Element("guid")?.Value is { Length: > 0 } guid
            ? PleskLookup<string>.Found(guid)
            : PleskLookup<string>.Unknown;
    }

    /// <summary>Moves a customer's subscription onto another service plan.</summary>
    /// <param name="connection">The server the customer is on.</param>
    /// <param name="ownerLogin">The customer's login.</param>
    /// <param name="planGuid">The guid of the plan to switch to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<PleskReply> SwitchSubscriptionPlanAsync(PleskConnection connection, string ownerLogin, string planGuid, CancellationToken ct) =>
        SendAsync(
            connection,
            new XElement("webspace", new XElement("switch-subscription",
                new XElement("filter", new XElement("owner-login", ownerLogin)),
                new XElement("plan-guid", planGuid))),
            ct);

    /// <summary>Finds an IP address every subscription on the server may share.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first shared address, or null when the server lists none or cannot be read.</returns>
    public async Task<string?> FindSharedIpAsync(PleskConnection connection, CancellationToken ct)
    {
        var reply = await SendAsync(connection, new XElement("ip", new XElement("get")), ct);
        return reply.Result?
            .Descendants()
            .FirstOrDefault(e => e.Element("ip_address") is not null
                && string.Equals(e.Element("type")?.Value, SharedIpType, StringComparison.OrdinalIgnoreCase))?
            .Element("ip_address")?.Value;
    }

    /// <summary>Changes one customer setting.</summary>
    /// <param name="connection">The server the customer is on.</param>
    /// <param name="login">The customer's login.</param>
    /// <param name="setting">The <c>gen_info</c> child to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    private Task<PleskReply> SetCustomerAsync(PleskConnection connection, string login, XElement setting, CancellationToken ct) =>
        SendAsync(connection, Customer("set", ByLogin(login), new XElement("values", new XElement("gen_info", setting))), ct);

    /// <summary>Sends one operation and reads its reply.</summary>
    /// <param name="connection">The server to send it to.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply; <see cref="PleskReply.Unreadable"/> on a transport failure or a body that is not a reply.</returns>
    private async Task<PleskReply> SendAsync(PleskConnection connection, XElement operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{connection.BaseUrl.TrimEnd('/')}{AgentPath}")
        {
            Content = new StringContent(PleskPacket.Build(operation), Encoding.UTF8, XmlMediaType),
        };
        request.Headers.Add(KeyHeader, connection.ApiKey);

        string body;
        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Plesk {Operation} answered HTTP {StatusCode}.", OperationName(operation), (int)response.StatusCode);
                return PleskReply.Unreadable;
            }

            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Plesk {Operation} could not be sent.", OperationName(operation));
            return PleskReply.Unreadable;
        }

        var reply = PleskPacket.Parse(body);
        if (!reply.IsSuccess)
        {
            _logger.LogWarning("Plesk {Operation} was refused with code {ErrorCode}.", OperationName(operation), reply.ErrorCode);
        }

        return reply;
    }

    /// <summary>Names an operation for a log line, e.g. <c>customer.get</c>.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The object and verb.</returns>
    private static string OperationName(XElement operation) =>
        $"{operation.Name}.{operation.Elements().FirstOrDefault()?.Name}";

    /// <summary>Builds a customer operation.</summary>
    /// <param name="verb">The verb, e.g. <c>get</c>.</param>
    /// <param name="content">The verb's children.</param>
    /// <returns>The operation.</returns>
    private static XElement Customer(string verb, params object[] content) =>
        new("customer", new XElement(verb, content));

    /// <summary>Builds a filter that selects one customer by login.</summary>
    /// <param name="login">The customer's login.</param>
    /// <returns>The filter.</returns>
    private static XElement ByLogin(string login) =>
        new("filter", new XElement("login", login));

    /// <summary>Builds a hosting property.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">Its value.</param>
    /// <returns>The property element.</returns>
    private static XElement Property(string name, string value) =>
        new("property", new XElement("name", name), new XElement("value", value));
}
