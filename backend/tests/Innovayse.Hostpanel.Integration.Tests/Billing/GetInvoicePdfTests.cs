using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Pdf;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/{id}/pdf</c> against Postgres: staff download any invoice as a PDF attachment,
/// drafts included and stamped, and the internal notes never appear on it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetInvoicePdfTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin downloads the invoice as a PDF attachment named after it.</summary>
    [Fact]
    public async Task GetPdf_ReturnsPdfAttachmentForAdmin()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().Be($"invoice-{invoiceId}.pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        var text = await PdfText.CompactAsync(response);
        text.Should().Contain($"Invoice#{invoiceId}");
        text.Should().Contain("Hosting");
        text.Should().Contain("AdaLovelace");
    }

    /// <summary>The PDF carries a client's billing details, so no browser or proxy may keep a copy.</summary>
    [Fact]
    public async Task GetPdf_IsNeverCached()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.NoStore.Should().BeTrue();
    }

    /// <summary>
    /// An invoice with more lines than <c>InvoicePdf:MaxLines</c> is refused before anything is
    /// rendered; one at the limit still renders.
    /// </summary>
    [Fact]
    public async Task GetPdf_RefusesInvoiceOverTheLineLimit()
    {
        var host = factory.WithWebHostBuilder(builder => builder.UseSetting("InvoicePdf:MaxLines", "2"));
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var atLimit = await CreateInvoiceWithLinesAsync(admin, clientId, 2);
        var overLimit = await CreateInvoiceWithLinesAsync(admin, clientId, 3);

        var rendered = await admin.GetAsync($"/api/billing/{atLimit}/pdf");
        var refused = await admin.GetAsync($"/api/billing/{overLimit}/pdf");

        rendered.StatusCode.Should().Be(HttpStatusCode.OK);
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(refused)).Should().Be(HostpanelErrorCodes.INVOICE_PDF_EXCEEDS_LIMIT);
    }

    /// <summary>
    /// With the only render slot taken for longer than <c>InvoicePdf:MaxRenderWaitSeconds</c>, the
    /// download answers 503 with the configured <c>Retry-After</c> instead of queueing on.
    /// </summary>
    [Fact]
    public async Task GetPdf_AnswersBusyWhenNoRenderSlotFrees()
    {
        using var host = BillingTestSeeds.RenderBusyHost(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);
        using var held = await host.Services.GetRequiredService<InvoicePdfRenderGate>().TryEnterAsync(CancellationToken.None);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(BillingTestSeeds.BusyRetryAfterSeconds));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVOICE_PDF_RENDER_BUSY);
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace().And.NotBe(HostpanelErrorCodes.INVOICE_PDF_RENDER_BUSY);
    }

    /// <summary>
    /// The action documents every status it can answer, with the error body on each refusal, so the
    /// generated API description matches what a caller can actually receive.
    /// </summary>
    [Fact]
    public void GetPdf_DeclaresEveryStatus()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.GetPdfAsync))!;

        var declared = action.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => (a.StatusCode, a.Type));

        declared.Should().BeEquivalentTo(new[]
        {
            (StatusCodes.Status200OK, typeof(FileContentResult)),
            (StatusCodes.Status400BadRequest, typeof(ErrorResponse)),
            (StatusCodes.Status401Unauthorized, typeof(ErrorResponse)),
            (StatusCodes.Status403Forbidden, typeof(ErrorResponse)),
            (StatusCodes.Status404NotFound, typeof(ErrorResponse)),
            (StatusCodes.Status503ServiceUnavailable, typeof(ErrorResponse)),
        });
    }

    /// <summary>A limit of zero or less is refused when the API starts, naming the setting.</summary>
    /// <param name="key">The <c>InvoicePdf</c> setting set to zero.</param>
    [Theory]
    [InlineData("InvoicePdf:MaxLines")]
    [InlineData("InvoicePdf:MaxConcurrentRenders")]
    [InlineData("InvoicePdf:MaxRenderWaitSeconds")]
    public void Startup_RefusesANonPositiveLimit(string key)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(key, "0"));

        var act = () => host.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage($"*{key}*");
    }

    /// <summary>An id that names no invoice answers 404 with the <c>INVOICE_NOT_FOUND</c> code.</summary>
    [Fact]
    public async Task GetPdf_UnknownId_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Downloading an invoice is not for anonymous callers.</summary>
    [Fact]
    public async Task GetPdf_Anonymous_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/1/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client cannot use the staff route, even for their own invoice.</summary>
    [Fact]
    public async Task GetPdf_ClientToken_Returns403()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "pdf-staff-route");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);

        var response = await client.GetAsync($"/api/billing/{invoiceId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Staff can download a draft, and it is stamped so nobody mistakes it for an issued invoice.</summary>
    [Fact]
    public async Task GetPdf_DraftForAdmin_IsStamped()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m, isDraft: true);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PdfText.CompactAsync(response)).Should().Contain("DRAFT");
    }

    /// <summary>The admins' internal notes stay off the PDF, which gets forwarded to customers.</summary>
    [Fact]
    public async Task GetPdf_LeavesOutInternalNotes()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);
        var notes = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/notes", new { invoiceId, notes = "INTERNAL-ONLY-7731" });
        notes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PdfText.CompactAsync(response)).Should().NotContain("INTERNAL-ONLY-7731");
    }

    /// <summary>An invoice in a currency with no row still renders, with the code written after each amount.</summary>
    [Fact]
    public async Task GetInvoicePdf_FallsBackWhenCurrencyRowMissing()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.Invoices
                .Where(i => i.Id == invoiceId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Currency, "XTS"));
        }

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PdfText.CompactAsync(response)).Should().Contain("12.50XTS");
    }

    /// <summary>Creates a published invoice with <paramref name="count"/> line items and returns its id.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="clientId">The client to bill.</param>
    /// <param name="count">How many line items it has.</param>
    /// <returns>The new invoice's primary key.</returns>
    private static async Task<int> CreateInvoiceWithLinesAsync(HttpClient admin, int clientId, int count)
    {
        var created = await admin.PostAsJsonAsync("/api/billing", new
        {
            clientId,
            dueDate = DateTimeOffset.UtcNow.AddDays(14),
            isDraft = false,
            items = Enumerable.Range(1, count)
                .Select(n => new { description = $"Line {n}", unitPrice = 1m, quantity = 1 })
                .ToArray(),
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return await created.Content.ReadFromJsonAsync<int>();
    }
}
