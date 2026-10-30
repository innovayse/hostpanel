using System.Net;
using System.Reflection;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/fulfilment/{recordId}/retry</c> against Postgres: a failed record is reset and
/// delivered by the kicked dispatch pass, a record that has not failed is a conflict, an unknown record or
/// one of another invoice is not found, and the route is staff only.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RetryFulfilmentRecordTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A failed record is reset with a fresh allowance and the kicked pass delivers it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Retry_FailedRecord_IsDeliveredByTheKickedPass()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        var recordId = await scenario.SeedFailedRecordAsync(invoiceId, FulfilmentKind.Paid, "plugin said no");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        HttpResponseMessage? response = null;
        Func<IMessageContext, Task> retry = async _ =>
        {
            response = await admin.PostAsync($"/api/billing/{invoiceId}/fulfilment/{recordId}/retry", content: null);
        };

        // The retry publishes the kicked pass; the session waits until that pass has run.
        await scenario.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(retry);

        response!.StatusCode.Should().Be(HttpStatusCode.NoContent);
        scenario.Plugin.Calls.Should().Equal([("OnInvoicePaidAsync", invoiceId)]);
        var record = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);
        record.Status.Should().Be(FulfilmentStatus.Done);
        record.Attempts.Should().Be(1);
        record.LastError.Should().BeNull();
    }

    /// <summary>A record that has not failed is refused with 409 and left as it was.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Retry_RecordThatHasNotFailed_Returns409()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        var recordId = (await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid)).Id;
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/fulfilment/{recordId}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.FULFILMENT_RECORD_NOT_RETRYABLE);
        (await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid)).Status.Should().Be(FulfilmentStatus.Pending);
    }

    /// <summary>An id that names no record is 404 with <c>FULFILMENT_RECORD_NOT_FOUND</c>.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Retry_UnknownRecord_Returns404()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        var response = await admin.PostAsync(
            $"/api/billing/{invoiceId}/fulfilment/{BillingTestSeeds.NoSuchId}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.FULFILMENT_RECORD_NOT_FOUND);
    }

    /// <summary>A record of another invoice is not found under this one, and stays failed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Retry_RecordOfAnotherInvoice_Returns404()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        var otherInvoiceId = await scenario.SeedInvoiceAsync();
        var recordId = await scenario.SeedFailedRecordAsync(otherInvoiceId, FulfilmentKind.Paid, "plugin said no");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/fulfilment/{recordId}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.FULFILMENT_RECORD_NOT_FOUND);
        (await scenario.ReadAsync(otherInvoiceId, FulfilmentKind.Paid)).Status.Should().Be(FulfilmentStatus.Failed);
    }

    /// <summary>A client cannot retry a record, even of their own invoice.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Retry_ClientToken_Returns403()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "fulfilment-retry-route");

        var response = await client.PostAsync("/api/billing/1/fulfilment/1/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The action declares every status it can answer, with the error body on each failure.</summary>
    [Fact]
    public void Retry_DeclaresEveryStatus()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.RetryFulfilmentAsync))!;

        var declared = action.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => (a.StatusCode, a.Type));

        declared.Should().BeEquivalentTo(new[]
        {
            (StatusCodes.Status204NoContent, typeof(void)),
            (StatusCodes.Status400BadRequest, typeof(ErrorResponse)),
            (StatusCodes.Status401Unauthorized, typeof(ErrorResponse)),
            (StatusCodes.Status403Forbidden, typeof(ErrorResponse)),
            (StatusCodes.Status404NotFound, typeof(ErrorResponse)),
            (StatusCodes.Status409Conflict, typeof(ErrorResponse)),
        });
    }
}
