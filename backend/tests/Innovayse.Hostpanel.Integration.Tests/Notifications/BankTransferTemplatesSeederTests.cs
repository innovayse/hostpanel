using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// The four seeded bank transfer emails exist in every language and print every value of the model their handler sends:
/// no placeholder is left unfilled, a bank detail the operator left empty is left out, and the Russian and Armenian versions
/// are really translated.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BankTransferTemplatesSeederTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The invoice every rendered model carries.</summary>
    private static readonly BankTransferInvoiceTemplateData Invoice =
        new(616161, 1234.56m, "USD", "INV-616161", "https://portal.example/client/invoices/616161");

    /// <summary>Renders one stored template's subject and body for a model and joins them.</summary>
    /// <param name="context">The data context holding the seeded templates.</param>
    /// <param name="slug">The template slug.</param>
    /// <param name="language">The language version.</param>
    /// <param name="model">The model the handler would send.</param>
    /// <returns>The rendered subject followed by the rendered body.</returns>
    private static async Task<string> RenderAsync(IHostpanelDbContext context, string slug, string language, EmailTemplateModel model)
    {
        var renderer = new TemplateRenderer(new FakeEmailBrandingProvider());
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == slug && t.Language == language, CancellationToken.None);
        var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);
        return $"{subject}\n{body}";
    }

    /// <summary>Asserts a rendered mail is clean and, for ru and hy, written in that language's script.</summary>
    /// <param name="text">The rendered mail.</param>
    /// <param name="language">The language version.</param>
    private static void AssertClean(string text, string language)
    {
        text.Should().NotContain("{{").And.NotContain("{%");
        if (language == "ru")
        {
            text.Should().MatchRegex("[Ѐ-ӿ]");
        }

        if (language == "hy")
        {
            text.Should().MatchRegex("[԰-֏]");
        }
    }

    /// <summary>The client mail prints the amount, the reference and every bank detail that is set, and skips the ones that are not.</summary>
    /// <param name="language">The language under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task Details_PrintsTheReferenceTheAmountAndOnlyTheDetailsThatAreSet(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await BankTransferTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var full = await RenderAsync(
            context, BankTransferTemplatesSeeder.DetailsSlug, language,
            new BankTransferDetailsTemplateModel(Invoice, new("Example LLC", "AM00 1111", "EXAMAMXX", "Example Bank", "Quote the reference.")));
        var sparse = await RenderAsync(
            context, BankTransferTemplatesSeeder.DetailsSlug, language,
            new BankTransferDetailsTemplateModel(Invoice, new(string.Empty, "AM00 1111", string.Empty, string.Empty, string.Empty)));

        full.Should().Contain("616161").And.Contain("INV-616161").And.Contain("1234.56").And.Contain("USD")
            .And.Contain("Example LLC").And.Contain("AM00 1111").And.Contain("EXAMAMXX").And.Contain("Example Bank").And.Contain("Quote the reference.");
        sparse.Should().Contain("AM00 1111").And.NotContain("EXAMAMXX").And.NotContain("Example LLC");
        AssertClean(full, language);
        AssertClean(sparse, language);
    }

    /// <summary>The staff notice names the invoice, the client, the reference and the amount.</summary>
    /// <param name="language">The language under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task RequestedAdmin_NamesTheInvoiceTheClientTheReferenceAndTheAmount(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await BankTransferTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var text = await RenderAsync(
            context, BankTransferTemplatesSeeder.RequestedAdminSlug, language,
            new BankTransferRequestedAdminTemplateModel(new InvoiceRefTemplateData(616161), new ClientRefTemplateData(770077), "INV-616161", 1234.56m, "USD"));

        text.Should().Contain("616161").And.Contain("770077").And.Contain("INV-616161").And.Contain("1234.56").And.Contain("USD");
        AssertClean(text, language);
    }

    /// <summary>The confirmation prints the reference and the amount that arrived.</summary>
    /// <param name="language">The language under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task Confirmed_PrintsTheReferenceAndTheAmount(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await BankTransferTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var text = await RenderAsync(
            context, BankTransferTemplatesSeeder.ConfirmedSlug, language, new BankTransferConfirmedTemplateModel(Invoice));

        text.Should().Contain("616161").And.Contain("INV-616161").And.Contain("1234.56").And.Contain("USD");
        AssertClean(text, language);
    }

    /// <summary>The rejection prints the reason, escaped, and the reference.</summary>
    /// <param name="language">The language under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task Rejected_PrintsTheReasonEscapedAndTheReference(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await BankTransferTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var text = await RenderAsync(
            context, BankTransferTemplatesSeeder.RejectedSlug, language,
            new BankTransferRejectedTemplateModel(Invoice, "Amount was <b>short</b>."));

        text.Should().Contain("INV-616161").And.Contain("616161").And.Contain("Amount was").And.NotContain("<b>short</b>");
        AssertClean(text, language);
    }
}
