using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/{id}/fulfilment</c> against Postgres: staff see an invoice's fulfilment records with
/// the plugin's error text, an unknown invoice is <c>INVOICE_NOT_FOUND</c>, and the route is staff only.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListInvoiceFulfilmentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff see every record of the invoice, with its state and the error that failed it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_ReturnsTheInvoiceRecordsWithTheirError()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        var failedId = await scenario.SeedFailedRecordAsync(invoiceId, FulfilmentKind.Refunded, "plugin said no");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/fulfilment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        rows.Should().HaveCount(2);
        var pending = rows.Single(r => r.GetProperty("kind").GetString() == nameof(FulfilmentKind.Paid));
        pending.GetProperty("status").GetString().Should().Be(nameof(FulfilmentStatus.Pending));
        pending.GetProperty("pluginId").GetString().Should().Be(scenario.PluginId);
        pending.GetProperty("canRetry").GetBoolean().Should().BeFalse();
        var failed = rows.Single(r => r.GetProperty("id").GetInt32() == failedId);
        failed.GetProperty("status").GetString().Should().Be(nameof(FulfilmentStatus.Failed));
        failed.GetProperty("attempts").GetInt32().Should().Be(FulfilmentRecord.MaxAttempts);
        failed.GetProperty("lastError").GetString().Should().Be("plugin said no");
        failed.GetProperty("canRetry").GetBoolean().Should().BeTrue();
    }

    /// <summary>An invoice no plugin was told about lists nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_InvoiceWithoutRecords_ReturnsAnEmptyList()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}/fulfilment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    /// <summary>An id that names no invoice is 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_UnknownInvoice_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/fulfilment");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>A client cannot read the records, even of their own invoice: the error text is staff only.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_ClientToken_Returns403()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "fulfilment-staff-route");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);

        var response = await client.GetAsync($"/api/billing/{invoiceId}/fulfilment");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Without a session the route answers 401.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/1/fulfilment");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The action declares every status it can answer, with the error body on each failure.</summary>
    [Fact]
    public void List_DeclaresEveryStatus()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.ListFulfilmentAsync))!;

        var declared = action.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => (a.StatusCode, a.Type));

        declared.Should().BeEquivalentTo(new[]
        {
            (StatusCodes.Status200OK, typeof(IReadOnlyList<FulfilmentRecordDto>)),
            (StatusCodes.Status400BadRequest, typeof(ErrorResponse)),
            (StatusCodes.Status401Unauthorized, typeof(ErrorResponse)),
            (StatusCodes.Status403Forbidden, typeof(ErrorResponse)),
            (StatusCodes.Status404NotFound, typeof(ErrorResponse)),
        });
    }
}
