using System.Net;
using System.Reflection;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/credit-notes/{id}/pdf</c> against Postgres: a client downloads their own notes as a PDF and nobody else's; a
/// stranger's note, a draft and an id that names nothing all answer the same 404, so ids cannot be probed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyCreditNotePdfTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The owner downloads their note as a PDF attachment named after its number.</summary>
    [Fact]
    public async Task GetPdf_OfTheCallersOwnNote_ReturnsThePdf()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-pdf-mine");
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 80m), 80m);
        var number = (await CreditNoteTestSeeds.NoteAsync(factory, noteId)).Number!;

        var response = await owner.GetAsync($"/api/me/credit-notes/{noteId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be($"credit-note-{number}.pdf");
        (await PdfText.CompactAsync(response)).Should().Contain($"Creditnote{number}");
    }

    /// <summary>Another client's note, a draft and an unknown id all answer 404 with the same code.</summary>
    [Fact]
    public async Task GetPdf_OfAStrangersNoteADraftOrNothing_AnswersAlike()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, ownerId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-pdf-own");
        var strangerId = await BillingTestSeeds.SeedClientAsync(factory);
        var strangersNote = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, strangerId, price: 50m), 50m);
        var ownInvoice = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, ownerId, price: 50m);
        var draft = await CreditNoteTestSeeds.DraftAsync(factory, ownInvoice);

        foreach (var id in new[] { strangersNote, draft, BillingTestSeeds.NoSuchId })
        {
            var response = await owner.GetAsync($"/api/me/credit-notes/{id}/pdf");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"note {id} is not the caller's to read");
            (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
        }
    }

    /// <summary>The route needs a client session.</summary>
    [Fact]
    public async Task GetPdf_NeedsAClientSession()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/me/credit-notes/1/pdf");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var asAdmin = await admin.GetAsync("/api/me/credit-notes/1/pdf");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The client actions document every status they can answer, with the error body on each refusal.</summary>
    [Fact]
    public void TheClientActions_DeclareEveryStatus()
    {
        var pdf = typeof(MyCreditNotesController).GetMethod(nameof(MyCreditNotesController.GetPdfAsync))!;
        var list = typeof(MyCreditNotesController).GetMethod(nameof(MyCreditNotesController.GetMineAsync))!;

        pdf.GetCustomAttributes<ProducesResponseTypeAttribute>().Select(a => (a.StatusCode, a.Type)).Should().BeEquivalentTo(new[]
        {
            (StatusCodes.Status200OK, typeof(FileContentResult)),
            (StatusCodes.Status400BadRequest, typeof(ErrorResponse)),
            (StatusCodes.Status401Unauthorized, typeof(ErrorResponse)),
            (StatusCodes.Status403Forbidden, typeof(ErrorResponse)),
            (StatusCodes.Status404NotFound, typeof(ErrorResponse)),
            (StatusCodes.Status503ServiceUnavailable, typeof(ErrorResponse)),
        });
        list.GetCustomAttributes<ProducesResponseTypeAttribute>().Select(a => a.StatusCode).Should().BeEquivalentTo(new[]
        {
            StatusCodes.Status200OK, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound,
        });
    }
}
