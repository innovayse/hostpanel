using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Hosting;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds what customers run: services in every lifecycle state (pending, active, suspended, terminated)
/// assigned to the servers of the right group, the disk and bandwidth figures the usage report reads for
/// them, and the cancellation requests that go with the ones being wound down. The status of a service
/// follows a fixed pattern and the state of its owner (a closed customer's services are terminated, a
/// suspended customer's are suspended), so every reset produces the same picture.
/// </summary>
internal sealed class DemoServiceSeeder
{
    /// <summary>The group name of the shared servers, as the server seeder created it.</summary>
    private const string SharedGroup = "Shared Hosting Cluster";

    /// <summary>The group name of the virtual servers.</summary>
    private const string VpsGroup = "VPS Cluster";

    /// <summary>The group name of the dedicated servers.</summary>
    private const string DedicatedGroup = "Dedicated Pool";

    /// <summary>The status given to the n-th service, repeating; mostly active, so the lists look healthy.</summary>
    private static readonly ServiceStatus[] StatusPattern =
    [
        ServiceStatus.Active, ServiceStatus.Active, ServiceStatus.Active, ServiceStatus.Suspended,
        ServiceStatus.Active, ServiceStatus.Pending, ServiceStatus.Active, ServiceStatus.Terminated,
        ServiceStatus.Active, ServiceStatus.Active, ServiceStatus.Suspended, ServiceStatus.Active,
    ];

    /// <summary>The prefixes of the extra sites a customer runs next to the main one.</summary>
    private static readonly string[] SitePrefixes = ["shop", "blog", "app", "mail", "docs", "portal", "store", "status", "help", "news", "api", "staging"];

    /// <summary>The reasons customers give for stopping a service, rotated over the requests.</summary>
    private static readonly string[] CancellationReasons =
    [
        "The project this site served has ended.",
        "Moving to a larger plan with another product.",
        "Consolidating several services into one.",
        "Budget cut; the service is no longer needed.",
    ];

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor dates are measured from.</param>
    public DemoServiceSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Seeds the services, usage figures and cancellation requests that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await _db.ClientServices.AnyAsync(ct))
        {
            await SeedServicesAsync(ct);
        }

        if (!await _db.DiskUsageStats.AnyAsync(ct))
        {
            await SeedDiskUsageAsync(ct);
        }

        if (!await _db.CancellationRequests.AnyAsync(ct))
        {
            await SeedCancellationsAsync(ct);
        }
    }

    /// <summary>How many services a customer runs: the demo client twelve, everyone else one to three.</summary>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <returns>The service count.</returns>
    private static int ServiceCountFor(int position) => position == 0 ? 12 : 1 + (position % 3);

    /// <summary>
    /// The status a service ends up in, following the pattern but never contradicting its owner. Only a hosting
    /// service is left pending, because provisioning is the one thing a pending service waits for and only a hosting
    /// account can be provisioned; any other product takes the active state in its place.
    /// </summary>
    /// <param name="client">The owner.</param>
    /// <param name="sequence">The service's running number.</param>
    /// <param name="type">The type of the product the service is of.</param>
    /// <returns>The status to move the service to.</returns>
    private static ServiceStatus StatusFor(Client client, int sequence, ProductType type)
    {
        var fromPattern = StatusPattern[sequence % StatusPattern.Length];
        if (fromPattern == ServiceStatus.Pending && !DemoCatalogueSeeder.IsHosting(type))
        {
            fromPattern = ServiceStatus.Active;
        }

        return client.Status switch
        {
            ClientStatus.Closed => ServiceStatus.Terminated,
            ClientStatus.Suspended when fromPattern == ServiceStatus.Active => ServiceStatus.Suspended,
            _ => fromPattern,
        };
    }

    /// <summary>Seeds the services of every customer in the states the pattern gives.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedServicesAsync(CancellationToken ct)
    {
        var clients = await _db.Clients.OrderBy(c => c.Id).ToListAsync(ct);
        var products = await _db.Products.Include(p => p.Prices)
            .Where(p => p.Type != ProductType.Domain && p.Status == ProductStatus.Active)
            .OrderBy(p => p.Id).ToListAsync(ct);
        if (clients.Count == 0 || products.Count == 0)
        {
            return;
        }

        var groups = await _db.ServerGroups.ToDictionaryAsync(g => g.Name, g => g.Id, ct);
        var servers = await _db.Servers.Where(s => !s.IsDisabled).OrderBy(s => s.Id).ToListAsync(ct);

        var sequence = 0;
        var serverTurn = 0;
        var renewalDemoPlaced = false;
        for (var c = 0; c < clients.Count; c++)
        {
            var client = clients[c];
            var currency = client.Currency ?? DemoCatalogueSeeder.BaseCurrency;

            for (var i = 0; i < ServiceCountFor(c); i++)
            {
                var product = products[((c * 3) + (i * 5)) % products.Count];
                var cycle = (c + i) % 3 == 0 ? BillingCycle.Annual : BillingCycle.Monthly;
                var price = product.PriceFor(currency, cycle) ?? 0m;
                var status = StatusFor(client, sequence, product.Type);
                var host = i == 0 ? DemoSites.HostOf(client) : $"{SitePrefixes[(c + i) % SitePrefixes.Length]}.{DemoSites.HostOf(client)}";
                var reference = status == ServiceStatus.Pending ? null : $"demo-account-{client.Id}-{i}";

                // Ordered a few days after the customer signed up, one service after another, not at the moment of seeding,
                // and never after the anchor for a customer who joined only a few days ago.
                var service = ClientService.Create(client.Id, product.Id, cycle, _today);
                service.Backdate(DemoAnchor.Clamp(client.CreatedAt.AddDays(2 + (3 * i)), client.CreatedAt, _today));
                if (status != ServiceStatus.Pending)
                {
                    service.Activate(reference!, service.CreatedAt, _today);
                }

                if (status == ServiceStatus.Suspended)
                {
                    service.Suspend(_today, "Unpaid invoice", true, SuspensionSource.Overdue);
                }
                else if (status == ServiceStatus.Terminated)
                {
                    service.Terminate(_today, "Cancelled by the customer", true);
                }

                var serverId = status == ServiceStatus.Pending ? null : PickServerId(product.Type, groups, servers, serverTurn);
                if (serverId is not null)
                {
                    serverTurn++;
                }

                var renewal = status switch
                {
                    ServiceStatus.Active => _today.AddDays(1 + (((c * 7) + (i * 11)) % 28)),
                    ServiceStatus.Suspended => _today.AddDays(-(3 + ((c + i) % 10))),
                    _ => (DateTimeOffset?)null,
                };

                // The demo client's first active service is always ten days from its renewal, inside the 14-day invoicing
                // window, so the renewal invoice flow can be shown on a fresh reset.
                if (c == 0 && status == ServiceStatus.Active && !renewalDemoPlaced)
                {
                    renewal = _today.AddDays(10);
                    renewalDemoPlaced = true;
                }

                service.ChangeBillingTerms(new ServiceBillingTerms(
                    BillingCycle: cycle,
                    Quantity: 1,
                    FirstPaymentAmount: price,
                    RecurringAmount: price,
                    NextRenewalAt: renewal,
                    PaymentMethod: PaymentMethodNames.Card,
                    PromotionCode: null,
                    SubscriptionId: null));
                service.RecordHostingAccount(new ServiceHostingAccount(
                    ServerId: serverId,
                    Domain: host,
                    ProvisioningRef: reference,
                    DedicatedIp: product.Type is ProductType.Vps or ProductType.Dedicated ? $"198.51.100.{100 + sequence}" : null,
                    Username: DemoCatalogueSeeder.IsHosting(product.Type) ? $"demo{client.Id}{i}" : null,
                    Password: null));
                service.UpdateAdminNotes(status == ServiceStatus.Suspended ? "Suspended by the billing run until the invoice is paid." : null);
                if (status == ServiceStatus.Terminated)
                {
                    service.CorrectTerminationDate(DemoAnchor.Clamp(_today.AddDays(-(10 + ((c + i) % 40))), service.CreatedAt, _today));
                }

                _db.ClientServices.Add(service);
                sequence++;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Picks the next server of the group a product type runs on, rotating so the load looks spread.</summary>
    /// <param name="type">The product type.</param>
    /// <param name="groups">Group ids by name.</param>
    /// <param name="servers">The enabled servers.</param>
    /// <param name="turn">How many servers have been handed out so far.</param>
    /// <returns>The server id, or null for a product that runs on no server of ours.</returns>
    private static int? PickServerId(ProductType type, IReadOnlyDictionary<string, int> groups, IReadOnlyList<Server> servers, int turn)
    {
        var groupName = type switch
        {
            ProductType.SharedHosting => SharedGroup,
            ProductType.Vps => VpsGroup,
            ProductType.Dedicated => DedicatedGroup,
            _ => null,
        };
        if (groupName is null || !groups.TryGetValue(groupName, out var groupId))
        {
            return null;
        }

        var inGroup = servers.Where(s => s.ServerGroupId == groupId).ToList();
        return inGroup.Count == 0 ? null : inGroup[turn % inGroup.Count].Id;
    }

    /// <summary>Seeds a disk and bandwidth reading for every active or suspended service that sits on a server.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedDiskUsageAsync(CancellationToken ct)
    {
        var random = DemoAnchor.NewRandom();
        var clients = await _db.Clients.ToDictionaryAsync(c => c.Id, ct);
        var servers = await _db.Servers.ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var products = await _db.Products.ToDictionaryAsync(p => p.Id, p => p.Type, ct);
        var services = await _db.ClientServices
            .Where(s => s.ServerId != null && (s.Status == ServiceStatus.Active || s.Status == ServiceStatus.Suspended))
            .OrderBy(s => s.Id).ToListAsync(ct);

        foreach (var service in services)
        {
            var client = clients[service.ClientId];
            var (diskLimit, bandwidthLimit) = products[service.ProductId] switch
            {
                ProductType.Dedicated => (960_000L, 10_240_000L),
                ProductType.Vps => (81_920L, 2_048_000L),
                _ => (25_600L, 1_024_000L),
            };

            // A suspended service sits near its quota, which is usually why it was looked at; the rest vary.
            var diskShare = service.Status == ServiceStatus.Suspended ? 97 : random.Next(5, 93);
            var bandwidthShare = service.Status == ServiceStatus.Suspended ? 91 : random.Next(2, 80);

            _db.DiskUsageStats.Add(DiskUsageStat.Create(
                servers[service.ServerId!.Value],
                service.Domain ?? DemoSites.HostOf(client),
                $"{client.FirstName} {client.LastName}",
                diskLimit * diskShare / 100,
                diskLimit,
                bandwidthLimit * bandwidthShare / 100,
                bandwidthLimit,
                _today));
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds cancellation requests: a closed one for every terminated service, an open end-of-period one for every
    /// suspended service, and open or withdrawn ones for some active services.
    /// </summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedCancellationsAsync(CancellationToken ct)
    {
        var services = await _db.ClientServices.OrderBy(s => s.Id).ToListAsync(ct);

        for (var n = 0; n < services.Count; n++)
        {
            var service = services[n];
            var reason = CancellationReasons[n % CancellationReasons.Length];

            // A request for a terminated service was made the day before it ended; the others were made in the last few weeks.
            var madeAt = DemoAnchor.Clamp(
                service.TerminatedAt?.AddDays(-1) ?? _today.AddDays(-(1 + ((n * 7) % 35))).AddHours(9 + (n % 8)), service.CreatedAt, _today);

            if (service.Status == ServiceStatus.Terminated)
            {
                var request = CancellationRequest.Create(service.Id, CancellationType.Immediate, reason);
                request.Backdate(madeAt);
                request.Close();
                _db.CancellationRequests.Add(request);
            }
            else if (service.Status == ServiceStatus.Suspended)
            {
                var request = CancellationRequest.Create(service.Id, CancellationType.EndOfBillingPeriod, reason);
                request.Backdate(madeAt);
                _db.CancellationRequests.Add(request);
            }
            else if (service.Status == ServiceStatus.Active && n % 6 == 0)
            {
                var type = n % 12 == 0 ? CancellationType.Immediate : CancellationType.EndOfBillingPeriod;
                var request = CancellationRequest.Create(service.Id, type, reason);
                request.Backdate(madeAt);
                _db.CancellationRequests.Add(request);
            }
            else if (service.Status == ServiceStatus.Active && n % 9 == 4)
            {
                // A customer who changed their mind: the request is closed and the service stays active.
                var withdrawn = CancellationRequest.Create(service.Id, CancellationType.EndOfBillingPeriod, "Withdrawn: " + reason);
                withdrawn.Backdate(madeAt);
                withdrawn.Close();
                _db.CancellationRequests.Add(withdrawn);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
