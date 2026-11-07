using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Pdf;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/credit-notes/{id}/pdf</c> against Postgres: staff download any note as a PDF attachment named after its
/// number, with the seller, the client, the lines and the reason on it, never cached; a note over the line limit and a busy
/// renderer are refused with their codes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetCreditNotePdfTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin downloads the note as a PDF attachment named after its number, with its content on it.</summary>
    [Fact]
    public async Task GetPdf_ReturnsAPdfAttachmentNamedAfterTheNumber()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m, taxRate: 20m);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 50m);
        var number = (await CreditNoteTestSeeds.NoteAsync(factory, noteId)).Number!;

        var response = await admin.GetAsync($"/api/billing/credit-notes/{noteId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().Be($"credit-note-{number}.pdf");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var text = await PdfText.CompactAsync(response);
        text.Should().Contain($"Creditnote{number}").And.Contain("Creditforhosting").And.Contain("AdaLovelace");
        text.Should().Contain(CreditNoteTestSeeds.Reason.Replace(" ", string.Empty)).And.Contain($"#{invoiceId}");
        text.Should().Contain("60.00");
    }

    /// <summary>A voided note downloads too, stamped as voided, so the record stays complete.</summary>
    [Fact]
    public async Task GetPdf_OfAVoidedNote_IsStamped()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 50m), 50m);
        (await admin.PostAsJsonAsync($"/api/billing/credit-notes/{noteId}/void", new { reason = "Mistake" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var response = await admin.GetAsync($"/api/billing/credit-notes/{noteId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PdfText.CompactAsync(response)).Should().Contain("VOIDED");
    }

    /// <summary>A note with more lines than <c>InvoicePdf:MaxLines</c> is refused before anything is rendered.</summary>
    [Fact]
    public async Task GetPdf_RefusesANoteOverTheLineLimit()
    {
        var host = factory.WithWebHostBuilder(builder => builder.UseSetting("InvoicePdf:MaxLines", "1"));
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var created = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit-notes", new
        {
            items = new[]
            {
                new { description = "One", unitPrice = 10m, quantity = 1 },
                new { description = "Two", unitPrice = 10m, quantity = 1 },
            },
            method = "AccountCredit",
            reason = "Two lines",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await admin.GetAsync($"/api/billing/credit-notes/{await created.Content.ReadFromJsonAsync<int>()}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_PDF_EXCEEDS_LIMIT);
    }

    /// <summary>With the only render slot taken, the download answers 503 with the configured <c>Retry-After</c>.</summary>
    [Fact]
    public async Task GetPdf_AnswersBusyWhenNoRenderSlotFrees()
    {
        using var host = BillingTestSeeds.RenderBusyHost(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(host, clientId, price: 50m), 50m);
        using var held = await host.Services.GetRequiredService<InvoicePdfRenderGate>().TryEnterAsync(CancellationToken.None);

        var response = await admin.GetAsync($"/api/billing/credit-notes/{noteId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(BillingTestSeeds.BusyRetryAfterSeconds));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVOICE_PDF_RENDER_BUSY);
    }

    /// <summary>An id that names no note answers 404 with its code.</summary>
    [Fact]
    public async Task GetPdf_UnknownId_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/credit-notes/{BillingTestSeeds.NoSuchId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
    }

    /// <summary>The staff download is not for anonymous callers or clients.</summary>
    [Fact]
    public async Task GetPdf_IsStaffOnly()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/billing/credit-notes/1/pdf");
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-pdf-staff");
        var asClient = await client.GetAsync("/api/billing/credit-notes/1/pdf");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Every credit note action documents every status it can answer, with the error body on each refusal.</summary>
    [Fact]
    public void TheAdminActions_DeclareEveryStatus()
    {
        var pdf = typeof(CreditNotesController).GetMethod(nameof(CreditNotesController.GetPdfAsync))!;
        var issue = typeof(CreditNotesController).GetMethod(nameof(CreditNotesController.IssueAsync))!;
        var voidAction = typeof(CreditNotesController).GetMethod(nameof(CreditNotesController.VoidAsync))!;

        Declared(pdf).Should().BeEquivalentTo(new[]
        {
            (StatusCodes.Status200OK, typeof(FileContentResult)),
            (StatusCodes.Status400BadRequest, typeof(ErrorResponse)),
            (StatusCodes.Status401Unauthorized, typeof(ErrorResponse)),
            (StatusCodes.Status403Forbidden, typeof(ErrorResponse)),
            (StatusCodes.Status404NotFound, typeof(ErrorResponse)),
            (StatusCodes.Status503ServiceUnavailable, typeof(ErrorResponse)),
        });
        Declared(issue).Select(d => d.Item1).Should().BeEquivalentTo(new[]
        {
            StatusCodes.Status201Created, StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status502BadGateway, StatusCodes.Status503ServiceUnavailable,
        });
        Declared(voidAction).Select(d => d.Item1).Should().BeEquivalentTo(new[]
        {
            StatusCodes.Status204NoContent, StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound, StatusCodes.Status409Conflict,
        });
    }

    /// <summary>Reads the statuses and body types an action declares.</summary>
    /// <param name="action">The controller action.</param>
    /// <returns>The status and type pairs.</returns>
    private static List<(int, Type)> Declared(MethodInfo action) =>
        [.. action.GetCustomAttributes<ProducesResponseTypeAttribute>().Select(a => (a.StatusCode, a.Type))];
}
