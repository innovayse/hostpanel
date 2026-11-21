using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Ssl;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the infrastructure the admin panel lists: server groups with servers, and certificate
/// check results. Hostnames sit under the reserved <c>.example</c> suffix and credentials are
/// placeholders; the demo adapters never use either.
/// </summary>
internal sealed class DemoServerSeeder
{
    /// <summary>The nameserver pair every server advertises.</summary>
    private const string PrimaryNameserver = "ns1.demo-dns.example";

    /// <summary>The second nameserver every server advertises.</summary>
    private const string SecondaryNameserver = "ns2.demo-dns.example";

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor certificate expiry dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor certificate expiry dates are measured from.</param>
    public DemoServerSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Seeds servers and certificate checks that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await _db.ServerGroups.AnyAsync(ct))
        {
            await SeedServersAsync(ct);
        }

        if (!await _db.SslChecks.AnyAsync(ct))
        {
            await SeedSslChecksAsync(ct);
        }
    }

    /// <summary>Seeds three server groups with seven servers: one offline, one disabled, the rest healthy.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedServersAsync(CancellationToken ct)
    {
        var shared = ServerGroup.Create("Shared Hosting Cluster", ServerFillType.LeastFull);
        var vps = ServerGroup.Create("VPS Cluster", ServerFillType.FillUntilFull);
        var dedicated = ServerGroup.Create("Dedicated Pool", ServerFillType.FillUntilFull);
        _db.ServerGroups.Add(shared);
        _db.ServerGroups.Add(vps);
        _db.ServerGroups.Add(dedicated);
        await _db.SaveChangesAsync(ct);

        // Every hosting product maps to the same server module, so a server of that module is what the selector can
        // pick when a service is set up; the variety is only there to fill the admin lists. TEST-NET addresses
        // (RFC 5737) are used because they can never route to a real machine.
        (string Name, string Host, string Ip, int MaxAccounts, bool IsDefault, bool IsDisabled, bool Online, decimal Cost, string Datacenter, int GroupId)[] servers =
        [
            ("Shared EU-01", "shared01.demo-hosting.example", "192.0.2.10", 500, true, false, true, 150m, "Frankfurt", shared.Id),
            ("Shared US-01", "shared02.demo-hosting.example", "192.0.2.11", 500, false, false, true, 150m, "New York", shared.Id),
            ("Shared EU-02", "shared03.demo-hosting.example", "192.0.2.12", 400, false, false, true, 130m, "Amsterdam", shared.Id),
            ("VPS EU-01", "vps01.demo-hosting.example", "192.0.2.20", 100, false, false, true, 350m, "Frankfurt", vps.Id),
            ("VPS US-01", "vps02.demo-hosting.example", "192.0.2.21", 100, false, false, false, 350m, "Chicago", vps.Id),
            ("Dedicated EU-01", "dedicated01.demo-hosting.example", "198.51.100.30", 20, false, false, true, 900m, "Helsinki", dedicated.Id),
            ("Dedicated US-01", "dedicated02.demo-hosting.example", "198.51.100.31", 20, false, true, true, 900m, "Dallas", dedicated.Id),
        ];

        for (var i = 0; i < servers.Length; i++)
        {
            var (name, host, ip, max, isDefault, isDisabled, online, cost, datacenter, groupId) = servers[i];
            var server = Server.Create(new ServerDetails(
                name, host, ip, null, ServerModule.Cwp7, "root", new ServerCredentials("demo-placeholder", null, "demo-placeholder-hash"),
                true, max, isDefault, isDisabled, cost, datacenter, null,
                new ServerNameServers(
                    PrimaryNameserver, "192.0.2.1", SecondaryNameserver, "192.0.2.2", null, null, null, null, null, null),
                AcceptsSelfSignedCertificate: false),
                _today);
            server.AssignToGroup(groupId);
            // Between a quarter and three quarters full, so an account count never passes the server's own limit.
            server.RecordConnectionTest(online, online ? max * (25 + ((i * 11) % 50)) / 100 : null, _today);
            _db.Servers.Add(server);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds one certificate check per customer site, spread across the expiry buckets of the report (expired, soon,
    /// later), with some sites on no certificate at all, plus one retired site that is no longer monitored.
    /// </summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedSslChecksAsync(CancellationToken ct)
    {
        var clients = await _db.Clients.OrderBy(c => c.Id).ToListAsync(ct);
        int[] expiryDays = [75, 21, 140, 310, 55, 365, 9, 200, 33, -4, 88, 12, 160, 47, 250];

        for (var i = 0; i < clients.Count; i++)
        {
            var host = DemoSites.HostOf(clients[i]);

            // Every seventh site has no certificate, so the "no SSL" bucket is never empty.
            if (i % 7 == 5)
            {
                _db.SslChecks.Add(SslCheck.Create(host, false, null, null, true, _today));
            }
            else
            {
                _db.SslChecks.Add(SslCheck.Create(
                    host, true, "Demo Certificate Authority", _today.AddDays(expiryDays[i % expiryDays.Length]), true, _today));
            }
        }

        _db.SslChecks.Add(SslCheck.Create("retired-site.example", false, null, null, false, _today));
        await _db.SaveChangesAsync(ct);
    }
}
