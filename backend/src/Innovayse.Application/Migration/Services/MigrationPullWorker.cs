namespace Innovayse.Application.Migration.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pulls data page-by-page from the migration plugin, persists each batch into the
/// Innovayse database, and updates the job's progress counters.
/// Runs in a background task via a dedicated DI scope.
/// </summary>
public sealed class MigrationPullWorker(
    IMigrationJobRepository repo,
    IMigrationLogRepository logRepo,
    IHttpClientFactory httpClientFactory,
    IUserService userService,
    IClientRepository clientRepo,
    IInvoiceRepository invoiceRepo,
    IClientServiceRepository serviceRepo,
    IDomainRepository domainRepo,
    ITicketRepository ticketRepo,
    IDepartmentRepository departmentRepo,
    IProductRepository productRepo,
    IProductGroupRepository productGroupRepo,
    IOrderRepository orderRepo,
    ITransactionRepository transactionRepo,
    IQuoteRepository quoteRepo,
    IKbArticleRepository kbArticleRepo,
    IKbCategoryRepository kbCategoryRepo,
    IAnnouncementRepository announcementRepo,
    IDownloadRepository downloadRepo,
    INetworkIssueRepository networkIssueRepo,
    IUnitOfWork uow,
    ILogger<MigrationPullWorker> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const int PerPage = 50;

    private int _jobId;

    /// <summary>Executes the full pull for a job identified by <paramref name="jobId"/>.</summary>
    public async Task RunAsync(int jobId, CancellationToken ct)
    {
        _jobId = jobId;
        try
        {
            await PullAsync(jobId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration pull failed for job {JobId}", jobId);
            try
            {
                // Clear any pending/failed EF change tracker state before saving failure
                uow.DetachAll();
                var job = await repo.GetByIdAsync(jobId, ct);
                if (job is not null)
                {
                    job.Fail(ex.Message);
                    await repo.SaveAsync(ct);
                }
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Failed to save failure state for job {JobId}", jobId);
            }
        }
    }

    // ── Main orchestration ────────────────────────────────────────────────────

    private async Task PullAsync(int jobId, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Migration job {jobId} not found.");

        var client = httpClientFactory.CreateClient("migration");

        // 1. Fetch totals from plugin
        var totalsResp = await PostActionAsync<TotalsResponse>(client, job.SourceUrl, job.Key, "totals", null, ct);
        job.Start(
            job.ExportClients ? totalsResp.Clients : 0,
            job.ExportInvoices ? totalsResp.Invoices : 0,
            job.ExportServices ? totalsResp.Services : 0,
            job.ExportDomains ? totalsResp.Domains : 0,
            job.ExportTickets ? totalsResp.Tickets : 0,
            job.ExportProducts ? totalsResp.Products : 0,
            job.ExportOrders ? totalsResp.Orders : 0,
            job.ExportTransactions ? totalsResp.Transactions : 0,
            job.ExportQuotes ? totalsResp.Quotes : 0,
            job.ExportKnowledgebase ? totalsResp.Knowledgebase : 0,
            job.ExportContacts ? totalsResp.Contacts : 0,
            job.ExportTicketReplies ? totalsResp.TicketReplies : 0,
            job.ExportAnnouncements ? totalsResp.Announcements : 0,
            job.ExportDownloads ? totalsResp.Downloads : 0,
            job.ExportNetworkIssues ? totalsResp.NetworkIssues : 0);
        await repo.SaveAsync(ct);

        // 2. Pull and import each entity type in dependency order:
        //    Clients first (invoices/services/domains/tickets reference them)
        if (job.ExportClients)
        {
            await PullClientsAsync(client, job, ct);
        }

        if (job.ExportProducts)
        {
            await PullProductsAsync(client, job, ct);
        }

        if (job.ExportInvoices)
        {
            await PullInvoicesAsync(client, job, ct);
        }

        if (job.ExportServices)
        {
            await PullServicesAsync(client, job, ct);
        }

        if (job.ExportDomains)
        {
            await PullDomainsAsync(client, job, ct);
        }

        if (job.ExportTickets)
        {
            await PullTicketsAsync(client, job, ct);
        }

        if (job.ExportOrders)
        {
            await PullOrdersAsync(client, job, ct);
        }

        if (job.ExportTransactions)
        {
            await PullTransactionsAsync(client, job, ct);
        }

        if (job.ExportQuotes)
        {
            await PullQuotesAsync(client, job, ct);
        }

        if (job.ExportKnowledgebase)
        {
            await PullKnowledgebaseAsync(client, job, ct);
        }

        if (job.ExportContacts)
        {
            await PullContactsAsync(client, job, ct);
        }

        if (job.ExportTicketReplies)
        {
            await PullTicketRepliesAsync(client, job, ct);
        }

        if (job.ExportAnnouncements)
        {
            await PullAnnouncementsAsync(client, job, ct);
        }

        if (job.ExportDownloads)
        {
            await PullDownloadsAsync(client, job, ct);
        }

        if (job.ExportNetworkIssues)
        {
            await PullNetworkIssuesAsync(client, job, ct);
        }

        job.Complete();
        await repo.SaveAsync(ct);
        logger.LogInformation("Migration job {JobId} completed successfully.", jobId);
    }

    // ── Clients ───────────────────────────────────────────────────────────────

    private async Task PullClientsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<ClientRecord>(http, job, "clients", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var result = await ImportClientAsync(rec, ct);
                    if (result == ImportResult.Imported)
                    {
                        imported++;
                    }
                    else if (result == ImportResult.Skipped)
                    {
                        skipped++;
                    }

                    await WriteLogAsync(MigrationEntityType.Clients, rec.Email, result, reason: null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped client {Email} during migration", rec.Email);
                    await WriteLogAsync(MigrationEntityType.Clients, rec.Email, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Clients, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    private async Task<ImportResult> ImportClientAsync(ClientRecord rec, CancellationToken ct)
    {
        var existing = await userService.FindByEmailAsync(rec.Email, ct);
        if (existing is not null)
        {
            logger.LogDebug("Client {Email} already exists — skipping", rec.Email);
            return ImportResult.Skipped;
        }

        var randomPassword = $"Mig@{Guid.NewGuid():N}"[..16] + "1!";
        var userId = await userService.CreateAsync(rec.Email, randomPassword, ct, rec.FirstName, rec.LastName);
        await userService.AddToRoleAsync(userId, Roles.Client, ct);

        var clientEntity = Client.Create(userId, rec.FirstName, rec.LastName, rec.Email, rec.Company);

        clientEntity.Update(rec.FirstName, rec.LastName, rec.Company, rec.Phone);

        if (!string.IsNullOrWhiteSpace(rec.Address) || !string.IsNullOrWhiteSpace(rec.City))
        {
            clientEntity.UpdateAddress(
                street: rec.Address ?? string.Empty,
                address2: null,
                city: rec.City ?? string.Empty,
                state: rec.State ?? string.Empty,
                postCode: rec.PostCode ?? string.Empty,
                country: rec.Country ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(rec.Currency))
        {
            clientEntity.UpdatePreferences(rec.Currency, null, null, null);
        }

        if (rec.TaxExempt)
        {
            clientEntity.UpdateSettings(
                lateFees: false, overdueNotices: false, taxExempt: true,
                separateInvoices: false, disableCcProcessing: false,
                marketingOptIn: false, statusUpdate: false, allowSso: false);
        }

        if (rec.CreditBalance > 0)
        {
            clientEntity.AddCredit(rec.CreditBalance);
        }

        clientRepo.Add(clientEntity);
        await uow.SaveChangesAsync(ct);
        return ImportResult.Imported;
    }

    // ── Invoices ──────────────────────────────────────────────────────────────

    private async Task PullInvoicesAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<InvoiceRecord>(http, job, "invoices", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var result = await ImportInvoiceAsync(rec, ct);
                    if (result.Action == ImportResult.Imported)
                    {
                        imported++;
                    }
                    else if (result.Action == ImportResult.Skipped)
                    {
                        skipped++;
                    }

                    await WriteLogAsync(MigrationEntityType.Invoices, rec.ClientEmail, result.Action, result.Reason, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped invoice for {Email} during migration", rec.ClientEmail);
                    await WriteLogAsync(MigrationEntityType.Invoices, rec.ClientEmail, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Invoices, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    private async Task<(ImportResult Action, string? Reason)> ImportInvoiceAsync(InvoiceRecord rec, CancellationToken ct)
    {
        var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            logger.LogWarning("Invoice skipped — client {Email} not found", rec.ClientEmail);
            return (ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found");
        }

        // Dedup by ExternalId (source system invoice ID)
        if (!string.IsNullOrWhiteSpace(rec.ExternalId))
        {
            var existing = await invoiceRepo.FindByExternalIdAsync(rec.ExternalId, ct);
            if (existing is not null)
            {
                return (ImportResult.Skipped, $"Invoice #{rec.ExternalId} already imported");
            }
        }

        // Draft invoices need a different factory — Create() produces Unpaid directly.
        var isDraft = rec.Status == "Draft";
        var invoice = isDraft
            ? Invoice.CreateDraft(clientId.Value, rec.DueDate)
            : Invoice.Create(clientId.Value, rec.DueDate); // starts as Unpaid

        foreach (var item in rec.Items)
        {
            invoice.AddItem(item.Description, item.Amount, item.Quantity);
        }

        if (!string.IsNullOrWhiteSpace(rec.PaymentMethod))
        {
            invoice.UpdateOptions(rec.Date, rec.DueDate, rec.PaymentMethod, 0);
        }

        switch (rec.Status)
        {
            case "Draft":
                // Already Draft from CreateDraft() — no transition needed
                break;
            case "Unpaid":
                // Already Unpaid from Create() — no transition needed
                break;
            case "Paid":
                invoice.MarkPaid("migrated");
                break;
            case "Overdue":
                invoice.MarkOverdue();
                break;
            case "Cancelled":
                invoice.Cancel();
                break;
            case "Refunded":
                invoice.MarkPaid("migrated");
                invoice.Refund();
                break;
            case "Collections":
                // No direct Collections transition — import as Overdue (closest equivalent)
                invoice.MarkOverdue();
                break;
            case "PaymentPending":
                // Payment initiated but not confirmed — already Unpaid
                break;
            default:
                break;
        }

        if (!string.IsNullOrWhiteSpace(rec.ExternalId))
        {
            invoice.SetExternalId(rec.ExternalId);
        }

        invoiceRepo.Add(invoice);
        await uow.SaveChangesAsync(ct);
        return (ImportResult.Imported, null);
    }

    // ── Services ──────────────────────────────────────────────────────────────

    private async Task PullServicesAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        var allProducts = await productRepo.ListAsync(null, false, ct);

        await foreach (var page in PagesAsync<ServiceRecord>(http, job, "services", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var result = await ImportServiceAsync(rec, allProducts, ct);
                    if (result.Action == ImportResult.Imported)
                    {
                        imported++;
                    }
                    else if (result.Action == ImportResult.Skipped)
                    {
                        skipped++;
                    }

                    await WriteLogAsync(MigrationEntityType.Services, rec.ClientEmail, result.Action, result.Reason, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped service for {Email} during migration", rec.ClientEmail);
                    await WriteLogAsync(MigrationEntityType.Services, rec.ClientEmail, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Services, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    private async Task<(ImportResult Action, string? Reason)> ImportServiceAsync(
        ServiceRecord rec,
        IReadOnlyList<Product> allProducts,
        CancellationToken ct)
    {
        var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            logger.LogWarning("Service skipped — client {Email} not found", rec.ClientEmail);
            return (ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found");
        }

        // Dedup by clientId + domain (if domain present)
        if (!string.IsNullOrWhiteSpace(rec.Domain))
        {
            var existingSvc = await serviceRepo.FindByClientAndDomainAsync(clientId.Value, rec.Domain, ct);
            if (existingSvc is not null)
            {
                return (ImportResult.Skipped, $"Service for domain '{rec.Domain}' already exists");
            }
        }

        var product = allProducts.FirstOrDefault(p =>
                          string.Equals(p.Name, rec.ProductName, StringComparison.OrdinalIgnoreCase))
                      ?? allProducts.FirstOrDefault();

        if (product is null)
        {
            logger.LogWarning("Service skipped — no products exist in the system");
            return (ImportResult.Skipped, "No products exist in the system");
        }

        var svc = ClientService.Create(clientId.Value, product.Id, rec.BillingCycle);

        svc.Update(
            domain: rec.Domain,
            dedicatedIp: null,
            username: rec.Username,
            password: null,
            billingCycle: rec.BillingCycle,
            recurringAmount: rec.RecurringAmount,
            paymentMethod: rec.PaymentMethod,
            nextRenewalAt: rec.NextDueDate,
            subscriptionId: rec.SubscriptionId,
            overrideAutoSuspend: false,
            suspendUntil: null,
            autoTerminateEndOfCycle: false,
            autoTerminateReason: null,
            adminNotes: rec.AdminNotes,
            provisioningRef: null,
            firstPaymentAmount: rec.FirstPaymentAmount,
            promotionCode: null,
            terminatedAt: rec.TerminatedAt,
            serverId: null,
            quantity: 1,
            productId: null);

        switch (rec.Status)
        {
            case "Active":
                svc.Activate("migrated");
                break;
            case "Suspended":
            case "Suspended (Admin)":
                svc.Activate("migrated");
                svc.Suspend();
                break;
            case "Terminated":
            case "Cancelled":
                svc.Activate("migrated");
                svc.Terminate();
                break;
        }

        serviceRepo.Add(svc);
        await uow.SaveChangesAsync(ct);
        return (ImportResult.Imported, null);
    }

    // ── Domains ───────────────────────────────────────────────────────────────

    private async Task PullDomainsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<DomainRecord>(http, job, "domains", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var result = await ImportDomainAsync(rec, ct);
                    if (result.Action == ImportResult.Imported)
                    {
                        imported++;
                    }
                    else if (result.Action == ImportResult.Skipped)
                    {
                        skipped++;
                    }

                    await WriteLogAsync(MigrationEntityType.Domains, rec.DomainName, result.Action, result.Reason, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped domain {Name} during migration", rec.DomainName);
                    await WriteLogAsync(MigrationEntityType.Domains, rec.DomainName, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Domains, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    private async Task<(ImportResult Action, string? Reason)> ImportDomainAsync(DomainRecord rec, CancellationToken ct)
    {
        var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            logger.LogWarning("Domain {Name} skipped — client {Email} not found", rec.DomainName, rec.ClientEmail);
            return (ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found");
        }

        var existing = await domainRepo.FindByNameAsync(rec.DomainName, ct);
        if (existing is not null)
        {
            logger.LogDebug("Domain {Name} already exists — skipping", rec.DomainName);
            return (ImportResult.Skipped, "Domain already exists");
        }

        var domain = Domain.Register(
            clientId: clientId.Value,
            name: rec.DomainName,
            expiresAt: rec.ExpiresAt,
            autoRenew: rec.AutoRenew,
            whoisPrivacy: false,
            registrar: rec.Registrar);

        // Apply status transitions — always start from PendingRegistration
        var whmcsStatus = (rec.Status ?? "active").ToLowerInvariant();
        switch (whmcsStatus)
        {
            case "expired":
                domain.Activate("migrated");
                domain.MarkExpired();
                break;
            case "redemption":
                domain.Activate("migrated");
                domain.MarkExpired();
                domain.MarkRedemption();
                break;
            case "transferred":
                domain.Activate("migrated");
                domain.MarkTransferred();
                break;
            case "cancelled":
            case "fraud":
                domain.Activate("migrated");
                domain.MarkExpired();
                domain.Cancel();
                break;
            default: // "active", "grace", "pending", etc.
                domain.Activate("migrated");
                break;
        }

        domain.Update(
            rec.FirstPaymentAmount, rec.RecurringAmount, rec.PaymentMethod,
            null, rec.SubscriptionId, rec.AdminNotes,
            rec.ExpiresAt, rec.NextDueDate ?? rec.ExpiresAt, 1, domain.Status);

        if (rec.Nameservers is { Count: > 0 })
        {
            domain.SetNameservers(rec.Nameservers);
        }

        domainRepo.Add(domain);
        await uow.SaveChangesAsync(ct);
        return (ImportResult.Imported, null);
    }

    // ── Tickets ───────────────────────────────────────────────────────────────

    private async Task PullTicketsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        var departments = await departmentRepo.ListAllAsync(ct);
        var defaultDeptId = departments.FirstOrDefault()?.Id ?? 1;

        await foreach (var page in PagesAsync<TicketRecord>(http, job, "tickets", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var deptId = departments.FirstOrDefault(d =>
                                     string.Equals(d.Name, rec.DepartmentName, StringComparison.OrdinalIgnoreCase))
                                 ?.Id ?? defaultDeptId;

                    var result = await ImportTicketAsync(rec, deptId, ct);
                    if (result.Action == ImportResult.Imported)
                    {
                        imported++;
                    }
                    else if (result.Action == ImportResult.Skipped)
                    {
                        skipped++;
                    }

                    await WriteLogAsync(MigrationEntityType.Tickets, rec.Subject, result.Action, result.Reason, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped ticket '{Subject}' during migration", rec.Subject);
                    await WriteLogAsync(MigrationEntityType.Tickets, rec.ClientEmail, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Tickets, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    private async Task<(ImportResult Action, string? Reason)> ImportTicketAsync(TicketRecord rec, int departmentId, CancellationToken ct)
    {
        var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            logger.LogWarning("Ticket skipped — client {Email} not found", rec.ClientEmail);
            return (ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found");
        }

        var existingTicket = await ticketRepo.FindByClientAndSubjectAsync(clientId.Value, rec.Subject, ct);
        if (existingTicket is not null)
        {
            return (ImportResult.Skipped, "Ticket already exists");
        }

        var priority = rec.Priority switch
        {
            "High" => TicketPriority.High,
            "Low" => TicketPriority.Low,
            _ => TicketPriority.Medium,
        };

        var ticket = Ticket.Create(
            clientId.Value,
            rec.Subject,
            string.IsNullOrWhiteSpace(rec.Message) ? "(no message)" : rec.Message,
            departmentId,
            priority);

        if (rec.Status is "Closed" or "Answered")
        {
            ticket.Close();
        }

        ticketRepo.Add(ticket);
        await uow.SaveChangesAsync(ct);
        return (ImportResult.Imported, null);
    }

    // ── Products ──────────────────────────────────────────────────────────────

    private async Task PullProductsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        // Step 1: fetch all product groups, create missing ones, build whmcsId→localId map
        var localGroups = await productGroupRepo.ListAsync(ct);
        var groupIdMap = new Dictionary<int, int>(); // whmcsId → localGroupId

        await foreach (var page in PagesAsync<ProductGroupRecord>(http, job, "product_groups", ct))
        {
            foreach (var grp in page)
            {
                var existing = localGroups.FirstOrDefault(g =>
                    string.Equals(g.Name, grp.Name, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    groupIdMap[grp.Id] = existing.Id;
                }
                else
                {
                    var newGroup = ProductGroup.Create(grp.Name, grp.Description);
                    productGroupRepo.Add(newGroup);
                    await uow.SaveChangesAsync(ct);
                    groupIdMap[grp.Id] = newGroup.Id;
                }
            }
        }

        // Step 2: page through products
        int imported = 0, skipped = 0;
        var allProducts = await productRepo.ListAsync(null, false, ct);

        await foreach (var page in PagesAsync<ProductRecord>(http, job, "products", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var exists = allProducts.Any(p =>
                        string.Equals(p.Name, rec.Name, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        await WriteLogAsync(MigrationEntityType.Products, rec.Name, ImportResult.Skipped, "Product already exists", ct);
                        skipped++;
                        continue;
                    }

                    if (!groupIdMap.TryGetValue(rec.GroupId, out var localGroupId))
                    {
                        await WriteLogAsync(MigrationEntityType.Products, rec.Name, ImportResult.Skipped, $"Group {rec.GroupId} not resolved", ct);
                        skipped++;
                        continue;
                    }

                    var product = Product.Create(
                        localGroupId,
                        rec.Name,
                        rec.Description,
                        null,
                        null,
                        null,
                        ProductType.Other,
                        rec.MonthlyPrice,
                        rec.AnnualPrice);

                    productRepo.Add(product);
                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.Products, rec.Name, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped product '{Name}' during migration", rec.Name);
                    await WriteLogAsync(MigrationEntityType.Products, rec.Name, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Products, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    // ── Orders ────────────────────────────────────────────────────────────────

    private async Task PullOrdersAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<OrderRecord>(http, job, "orders", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
                    if (clientId is null)
                    {
                        logger.LogWarning("Order skipped — client {Email} not found", rec.ClientEmail);
                        await WriteLogAsync(MigrationEntityType.Orders, rec.OrderNumber, ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found", ct);
                        skipped++;
                        continue;
                    }

                    var existingOrder = await orderRepo.FindByOrderNumberAsync(rec.OrderNumber, ct);
                    if (existingOrder is not null)
                    {
                        await WriteLogAsync(MigrationEntityType.Orders, rec.OrderNumber, ImportResult.Skipped, "Order already exists", ct);
                        skipped++;
                        continue;
                    }

                    var order = Order.Create(rec.OrderNumber, clientId.Value, rec.PaymentMethod, null);

                    if (rec.Items is { Count: > 0 })
                    {
                        var allProducts = await productRepo.ListAsync(null, false, ct);
                        foreach (var item in rec.Items)
                        {
                            // Resolve by name — product IDs differ from local IDs
                            var product = allProducts.FirstOrDefault(p =>
                                              string.Equals(p.Name, item.ProductName, StringComparison.OrdinalIgnoreCase))
                                          ?? allProducts.FirstOrDefault();

                            if (product is not null)
                            {
                                order.AddItem(product.Id, item.ProductName, item.BillingCycle,
                                    item.FirstPaymentAmount, item.RecurringAmount,
                                    item.Domain, item.Hostname);
                            }
                        }
                    }

                    switch (rec.Status)
                    {
                        case "Active":
                            order.Accept();
                            break;
                        case "Cancelled":
                            order.Cancel();
                            break;
                    }

                    orderRepo.Add(order);
                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.Orders, rec.OrderNumber, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped order {OrderNumber} during migration", rec.OrderNumber);
                    await WriteLogAsync(MigrationEntityType.Orders, rec.OrderNumber, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Orders, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    // ── Transactions ──────────────────────────────────────────────────────────

    private async Task PullTransactionsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<TransactionRecord>(http, job, "transactions", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
                    if (clientId is null)
                    {
                        logger.LogWarning("Transaction skipped — client {Email} not found", rec.ClientEmail);
                        await WriteLogAsync(MigrationEntityType.Transactions, rec.TransactionId, ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found", ct);
                        skipped++;
                        continue;
                    }

                    var existingTx = await transactionRepo.FindByTransactionIdAsync(rec.TransactionId, ct);
                    if (existingTx is not null)
                    {
                        await WriteLogAsync(MigrationEntityType.Transactions, rec.TransactionId, ImportResult.Skipped, "Transaction already exists", ct);
                        skipped++;
                        continue;
                    }

                    var transaction = Transaction.Create(
                        clientId.Value,
                        rec.Date,
                        rec.Description,
                        rec.TransactionId,
                        rec.InvoiceId,
                        rec.PaymentMethod,
                        rec.AmountIn,
                        rec.AmountOut,
                        rec.Fees,
                        false,
                        rec.Currency);

                    transactionRepo.Add(transaction);
                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.Transactions, rec.TransactionId, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped transaction {TransactionId} during migration", rec.TransactionId);
                    await WriteLogAsync(MigrationEntityType.Transactions, rec.TransactionId, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Transactions, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    // ── Quotes ────────────────────────────────────────────────────────────────

    private async Task PullQuotesAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<QuoteRecord>(http, job, "quotes", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
                    if (clientId is null)
                    {
                        logger.LogWarning("Quote skipped — client {Email} not found", rec.ClientEmail);
                        await WriteLogAsync(MigrationEntityType.Quotes, rec.Subject, ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found", ct);
                        skipped++;
                        continue;
                    }

                    var existingQuote = await quoteRepo.FindByClientAndSubjectAsync(clientId.Value, rec.Subject, ct);
                    if (existingQuote is not null)
                    {
                        await WriteLogAsync(MigrationEntityType.Quotes, rec.Subject, ImportResult.Skipped, "Quote already exists", ct);
                        skipped++;
                        continue;
                    }

                    var quote = Quote.Create(clientId.Value, rec.Subject, rec.ExpiryDate,
                        proposalText: rec.ProposalText,
                        customerNotes: rec.CustomerNotes);

                    foreach (var item in rec.Items)
                    {
                        quote.AddItem(item.Description, item.UnitPrice, item.Quantity, item.DiscountPercent);
                    }

                    switch (rec.Stage)
                    {
                        case "Delivered":
                            quote.Deliver();
                            break;
                        case "Accepted":
                            quote.Accept();
                            break;
                        case "Lost":
                            quote.MarkLost();
                            break;
                        case "Dead":
                            quote.MarkDead();
                            break;
                    }

                    quoteRepo.Add(quote);
                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.Quotes, rec.Subject, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped quote '{Subject}' during migration", rec.Subject);
                    await WriteLogAsync(MigrationEntityType.Quotes, rec.Subject, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Quotes, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    // ── Knowledgebase ─────────────────────────────────────────────────────────

    private async Task PullKnowledgebaseAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        // Step 1: fetch categories, create missing ones, build whmcsCatId→localCategoryName map
        var localCats = await kbCategoryRepo.ListAllAsync(ct);
        var catNameMap = new Dictionary<int, string>(); // whmcsCatId → local category name

        await foreach (var page in PagesAsync<KbCategoryRecord>(http, job, "knowledgebase_categories", ct))
        {
            foreach (var cat in page)
            {
                var existing = localCats.FirstOrDefault(c =>
                    string.Equals(c.Name, cat.Name, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    catNameMap[cat.Id] = existing.Name;
                }
                else
                {
                    var newCat = KbCategory.Create(cat.Name, cat.Description, false, null);
                    kbCategoryRepo.Add(newCat);
                    await uow.SaveChangesAsync(ct);
                    catNameMap[cat.Id] = newCat.Name;
                }
            }
        }

        // Step 2: page through articles
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<KbArticleRecord>(http, job, "knowledgebase", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var categoryName = rec.CategoryId.HasValue && catNameMap.TryGetValue(rec.CategoryId.Value, out var n) && !string.IsNullOrWhiteSpace(n)
                        ? n
                        : "General";

                    var existingArticle = await kbArticleRepo.FindByTitleAsync(rec.Title, ct);
                    if (existingArticle is not null)
                    {
                        await WriteLogAsync(MigrationEntityType.Knowledgebase, rec.Title, ImportResult.Skipped, "Article already exists", ct);
                        skipped++;
                        continue;
                    }

                    var article = KbArticle.Create(rec.Title, rec.Content, categoryName);

                    article.Publish();

                    kbArticleRepo.Add(article);
                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.Knowledgebase, rec.Title, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped KB article '{Title}' during migration", rec.Title);
                    await WriteLogAsync(MigrationEntityType.Knowledgebase, rec.Title, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Knowledgebase, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    // ── Contacts ──────────────────────────────────────────────────────────────

    private async Task PullContactsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;
        bool hasAny = false;

        await foreach (var page in PagesAsync<ContactRecord>(http, job, "contacts", ct))
        {
            hasAny = true;
            foreach (var rec in page)
            {
                try
                {
                    var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
                    if (clientId is null)
                    {
                        logger.LogWarning("Contact skipped — client {Email} not found", rec.ClientEmail);
                        await WriteLogAsync(MigrationEntityType.Contacts, rec.Email, ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found", ct);
                        skipped++;
                        continue;
                    }

                    var client2 = await clientRepo.FindByIdAsync(clientId.Value, ct);
                    if (client2 is null)
                    {
                        await WriteLogAsync(MigrationEntityType.Contacts, rec.Email, ImportResult.Skipped, "Client record not found", ct);
                        skipped++;
                        continue;
                    }

                    if (client2.Contacts.Any(c => string.Equals(c.Email, rec.Email, StringComparison.OrdinalIgnoreCase)))
                    {
                        await WriteLogAsync(MigrationEntityType.Contacts, rec.Email, ImportResult.Skipped, "Contact already exists", ct);
                        skipped++;
                        continue;
                    }

                    client2.AddContact(
                        rec.FirstName, rec.LastName, rec.Company,
                        rec.Email, rec.Phone, Innovayse.Domain.Clients.ContactType.General,
                        rec.Address, null, rec.City, rec.State, rec.PostCode, rec.Country,
                        true, true, true, true, true, false);

                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.Contacts, rec.Email, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped contact {Email} during migration", rec.Email);
                    await WriteLogAsync(MigrationEntityType.Contacts, rec.Email, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Contacts, imported, skipped);
            await repo.SaveAsync(ct);
        }

        if (!hasAny)
        {
            logger.LogInformation("No contacts found in WHMCS — skipping.");
        }
    }

    // ── Ticket Replies ────────────────────────────────────────────────────────

    private async Task PullTicketRepliesAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;
        bool hasAny = false;

        await foreach (var page in PagesAsync<TicketReplyRecord>(http, job, "ticket_replies", ct))
        {
            hasAny = true;
            foreach (var rec in page)
            {
                try
                {
                    var clientId = await ResolveClientIdAsync(rec.ClientEmail, ct);
                    if (clientId is null)
                    {
                        logger.LogWarning("Ticket reply skipped — client {Email} not found", rec.ClientEmail);
                        await WriteLogAsync(MigrationEntityType.TicketReplies, rec.TicketSubject, ImportResult.Skipped, $"Client '{rec.ClientEmail}' not found", ct);
                        skipped++;
                        continue;
                    }

                    var tickets = await ticketRepo.ListByClientIdAsync(clientId.Value, ct);
                    var ticket = tickets.FirstOrDefault(t =>
                        string.Equals(t.Subject, rec.TicketSubject, StringComparison.OrdinalIgnoreCase));

                    if (ticket is null)
                    {
                        await WriteLogAsync(MigrationEntityType.TicketReplies, rec.TicketSubject, ImportResult.Skipped, "Ticket not found", ct);
                        skipped++;
                        continue;
                    }

                    if (ticket.Status == Innovayse.Domain.Support.TicketStatus.Closed)
                    {
                        await WriteLogAsync(MigrationEntityType.TicketReplies, rec.TicketSubject, ImportResult.Skipped, "Ticket is closed", ct);
                        skipped++;
                        continue;
                    }

                    ticket.AddReply(
                        string.IsNullOrWhiteSpace(rec.Message) ? "(no message)" : rec.Message,
                        string.IsNullOrWhiteSpace(rec.AuthorName) ? "Unknown" : rec.AuthorName,
                        rec.IsStaffReply);

                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.TicketReplies, rec.TicketSubject, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped ticket reply for '{Subject}' during migration", rec.TicketSubject);
                    await WriteLogAsync(MigrationEntityType.TicketReplies, rec.TicketSubject, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.TicketReplies, imported, skipped);
            await repo.SaveAsync(ct);
        }

        if (!hasAny)
        {
            logger.LogInformation("No ticket replies found in WHMCS — skipping.");
        }
    }

    // ── Announcements ─────────────────────────────────────────────────────────

    private async Task PullAnnouncementsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<AnnouncementRecord>(http, job, "announcements", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var result = await ImportAnnouncementAsync(rec, ct);
                    if (result == ImportResult.Imported)
                    {
                        imported++;
                    }
                    else if (result == ImportResult.Skipped)
                    {
                        skipped++;
                    }

                    await WriteLogAsync(MigrationEntityType.Announcements, rec.Title, result, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped announcement '{Title}' during migration", rec.Title);
                    await WriteLogAsync(MigrationEntityType.Announcements, rec.Title, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Announcements, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    private async Task<ImportResult> ImportAnnouncementAsync(AnnouncementRecord rec, CancellationToken ct)
    {
        var announcement = Announcement.Create(rec.Title, rec.Content, rec.Published);
        announcementRepo.Add(announcement);
        await uow.SaveChangesAsync(ct);
        return ImportResult.Imported;
    }

    // ── Downloads ─────────────────────────────────────────────────────────────

    private async Task PullDownloadsAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        // Step 1: categories
        var catIdMap = new Dictionary<int, int>(); // whmcsCatId → localCatId

        await foreach (var page in PagesAsync<DownloadCategoryRecord>(http, job, "download_categories", ct))
        {
            foreach (var cat in page)
            {
                var newCat = DownloadCategory.Create(cat.Name, cat.Description ?? string.Empty, cat.Hidden, null);
                downloadRepo.AddCategory(newCat);
                await uow.SaveChangesAsync(ct);
                catIdMap[cat.Id] = newCat.Id;
            }
        }

        // Step 2: files
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<DownloadRecord>(http, job, "downloads", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var catId = rec.CategoryId.HasValue && catIdMap.TryGetValue(rec.CategoryId.Value, out var localCatId)
                        ? localCatId
                        : catIdMap.Values.FirstOrDefault();

                    var resolvedTitle = !string.IsNullOrWhiteSpace(rec.Title) ? rec.Title
                        : !string.IsNullOrWhiteSpace(rec.Filename) ? rec.Filename
                        : "Untitled";

                    var download = Download.Create(
                        resolvedTitle,
                        rec.Description ?? string.Empty,
                        rec.Type,
                        rec.Filename,
                        catId,
                        rec.ClientsOnly,
                        false,
                        rec.Hidden);

                    downloadRepo.Add(download);
                    await uow.SaveChangesAsync(ct);
                    imported++;
                    await WriteLogAsync(MigrationEntityType.Downloads, rec.Title, ImportResult.Imported, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped download '{Title}' during migration", rec.Title);
                    await WriteLogAsync(MigrationEntityType.Downloads, rec.Title, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.Downloads, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    // ── Network Issues ────────────────────────────────────────────────────────

    private async Task PullNetworkIssuesAsync(HttpClient http, MigrationJob job, CancellationToken ct)
    {
        int imported = 0, skipped = 0;

        await foreach (var page in PagesAsync<NetworkIssueRecord>(http, job, "network_issues", ct))
        {
            foreach (var rec in page)
            {
                try
                {
                    var result = await ImportNetworkIssueAsync(rec, ct);
                    if (result == ImportResult.Imported)
                    {
                        imported++;
                    }
                    else if (result == ImportResult.Skipped)
                    {
                        skipped++;
                    }

                    await WriteLogAsync(MigrationEntityType.NetworkIssues, rec.Title, result, null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipped network issue '{Title}' during migration", rec.Title);
                    await WriteLogAsync(MigrationEntityType.NetworkIssues, rec.Title, ImportResult.Failed, ex.Message, ct);
                }
            }

            job.UpdateProgress(MigrationEntityType.NetworkIssues, imported, skipped);
            await repo.SaveAsync(ct);
        }
    }

    private async Task<ImportResult> ImportNetworkIssueAsync(NetworkIssueRecord rec, CancellationToken ct)
    {
        var type = rec.Type?.ToLowerInvariant() switch
        {
            "server" => NetworkIssueType.Server,
            _ => NetworkIssueType.Other,
        };

        var priority = rec.Priority?.ToLowerInvariant() switch
        {
            "critical" => NetworkIssuePriority.Critical,
            "high" => NetworkIssuePriority.High,
            "low" => NetworkIssuePriority.Low,
            _ => NetworkIssuePriority.Medium,
        };

        var issue = NetworkIssue.Create(
            rec.Title,
            type,
            server: null,
            priority,
            rec.StartDate ?? DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(rec.Description) ? "(no description)" : rec.Description);

        if (rec.Status?.ToLowerInvariant() == "resolved")
        {
            issue.Resolve();
        }

        networkIssueRepo.Add(issue);
        await uow.SaveChangesAsync(ct);
        return ImportResult.Imported;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Looks up the Innovayse Client.Id for a given email address.</summary>
    private async Task<int?> ResolveClientIdAsync(string email, CancellationToken ct)
    {
        var user = await userService.FindByEmailAsync(email, ct);
        if (user is null)
        {
            return null;
        }

        var (userId, _) = user.Value;
        var client = await clientRepo.FindByUserIdAsync(userId, ct);
        return client?.Id;
    }

    /// <summary>Writes a single log entry and flushes it immediately.</summary>
    private async Task WriteLogAsync(
        MigrationEntityType entityType,
        string identifier,
        ImportResult action,
        string? reason,
        CancellationToken ct)
    {
        var logAction = action switch
        {
            ImportResult.Imported => MigrationLogAction.Imported,
            ImportResult.Skipped => MigrationLogAction.Skipped,
            _ => MigrationLogAction.Failed,
        };

        logRepo.Add(MigrationLog.Create(_jobId, entityType, identifier, logAction, reason));
        await logRepo.SaveAsync(ct);
    }

    /// <summary>
    /// Async iterator: fetches pages from the plugin and yields each page's item list.
    /// </summary>
    private async IAsyncEnumerable<IReadOnlyList<T>> PagesAsync<T>(
        HttpClient http,
        MigrationJob job,
        string action,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        int page = 1;

        while (true)
        {
            var extra = new Dictionary<string, object> { ["page"] = page, ["perPage"] = PerPage };
            var resp = await PostActionAsync<PagedResponse<T>>(http, job.SourceUrl, job.Key, action, extra, ct);

            if (resp.Items is { Count: > 0 })
            {
                yield return resp.Items;
            }

            if (page >= resp.TotalPages || resp.TotalPages == 0)
            {
                break;
            }

            page++;
        }
    }

    private async Task<T> PostActionAsync<T>(
        HttpClient client,
        string sourceUrl,
        string key,
        string action,
        Dictionary<string, object>? extra,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, object> { ["key"] = key, ["action"] = action };
        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                payload[kv.Key] = kv.Value;
            }
        }

        var response = await client.PostAsJsonAsync(sourceUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("WHMCS api.php returned {Status} for action '{Action}': {Body}",
                (int)response.StatusCode, action, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Empty response for action '{action}'.");
    }

    // ── Internal enum ─────────────────────────────────────────────────────────

    private enum ImportResult { Imported, Skipped, Failed }

    // ── Response DTOs ─────────────────────────────────────────────────────────

    private sealed class TotalsResponse
    {
        public int Clients { get; set; }
        public int Invoices { get; set; }
        public int Services { get; set; }
        public int Domains { get; set; }
        public int Tickets { get; set; }
        public int Products { get; set; }
        public int Orders { get; set; }
        public int Transactions { get; set; }
        public int Quotes { get; set; }
        public int Knowledgebase { get; set; }
        public int Contacts { get; set; }
        public int TicketReplies { get; set; }
        public int Announcements { get; set; }
        public int Downloads { get; set; }
        public int DownloadCategories { get; set; }
        public int NetworkIssues { get; set; }
    }

    private sealed class PagedResponse<T>
    {
        public List<T>? Items { get; set; }
        public int TotalPages { get; set; }
    }

    // ── Plugin record shapes (mapped from JSON) ───────────────────────────────

    private sealed record ClientRecord(
        string Email, string FirstName, string LastName,
        string? Company, string? Phone,
        string? Address, string? City, string? State, string? PostCode, string? Country,
        string Status, string? Currency, bool TaxExempt, decimal CreditBalance,
        DateTimeOffset? SignupDate, DateTimeOffset? LastLogin);

    private sealed record InvoiceRecord(
        string ClientEmail, string? ExternalId, decimal Total, string Status,
        DateTimeOffset Date, DateTimeOffset DueDate,
        string? PaymentMethod,
        List<InvoiceItemRecord> Items);

    private sealed record InvoiceItemRecord(string Description, decimal Amount, int Quantity);

    private sealed record ServiceRecord(
        string ClientEmail, string ProductName, string BillingCycle, string Status,
        string? Domain, string? Username, DateTimeOffset? NextDueDate,
        DateTimeOffset? TerminatedAt, string? SubscriptionId, string? AdminNotes,
        decimal RecurringAmount, decimal FirstPaymentAmount, string? PaymentMethod);

    private sealed record DomainRecord(
        string ClientEmail, string DomainName,
        DateTimeOffset RegisteredAt, DateTimeOffset ExpiresAt,
        DateTimeOffset? NextDueDate, string? Registrar, List<string>? Nameservers,
        string? Status, bool AutoRenew, decimal RecurringAmount, decimal FirstPaymentAmount,
        string? PaymentMethod, string? SubscriptionId, string? AdminNotes);

    private sealed record TicketRecord(
        string ClientEmail, string Subject, string Message,
        string Status, string Priority, string? DepartmentName, DateTimeOffset CreatedAt);

    private sealed record ProductGroupRecord(int Id, string Name, string? Description);
    private sealed record ProductRecord(int Id, int GroupId, string Name, string? Description, string Type, decimal MonthlyPrice, decimal AnnualPrice);
    private sealed record OrderRecord(string ClientEmail, string OrderNumber, string PaymentMethod, string Status, decimal Amount, DateTimeOffset CreatedAt, List<OrderItemRecord>? Items);
    private sealed record OrderItemRecord(int? ProductId, string ProductName, string BillingCycle, decimal FirstPaymentAmount, decimal RecurringAmount, string? Domain, string? Hostname);
    private sealed record TransactionRecord(string ClientEmail, DateTimeOffset Date, string Description, string TransactionId, int? InvoiceId, string PaymentMethod, decimal AmountIn, decimal AmountOut, decimal Fees, string? Currency);
    private sealed record QuoteRecord(string ClientEmail, string Subject, string Stage, DateTimeOffset ExpiryDate, string? ProposalText, string? CustomerNotes, List<QuoteItemRecord> Items, DateTimeOffset CreatedAt);
    private sealed record QuoteItemRecord(string Description, decimal UnitPrice, int Quantity, decimal DiscountPercent);
    private sealed record KbCategoryRecord(int Id, string Name, string Description, int? ParentId);
    private sealed record KbArticleRecord(string Title, string Content, int? CategoryId, bool Published);

    private sealed record ContactRecord(
        string ClientEmail, string FirstName, string LastName, string? Company,
        string Email, string? Phone, string? Address, string? City, string? State, string? PostCode, string? Country);

    private sealed record TicketReplyRecord(
        string ClientEmail, string TicketSubject, string Message, string AuthorName, bool IsStaffReply);

    private sealed record AnnouncementRecord(
        string Title, string Content, bool Published, DateTimeOffset? CreatedAt);

    private sealed record DownloadCategoryRecord(
        int Id, string Name, string? Description, bool Hidden, int? ParentId);

    private sealed record DownloadRecord(
        string Title, string? Description, string Type, string Filename,
        int? CategoryId, bool ClientsOnly, bool Hidden);

    private sealed record NetworkIssueRecord(
        string Title, string Type, string Status, string Priority,
        string Description, DateTimeOffset? StartDate, DateTimeOffset? EndDate);
}
