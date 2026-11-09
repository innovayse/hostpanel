using System.Reflection;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <see cref="TemplateRenderer"/>: typed models render under their lower snake case
/// names, the brand and site objects are there for every template, a body encodes every printed
/// value as HTML and a subject prints it as it is.
/// </summary>
/// <remarks>
/// Branding comes from <see cref="FakeEmailBrandingProvider"/>: <c>IEmailBrandingProvider</c> is a
/// port this product owns, and the rule is no <c>Moq</c> of our own code.
/// </remarks>
public sealed class TemplateRendererTests
{
    /// <summary>A ticket subject a visitor could type to plant a link and a script in staff mail.</summary>
    private const string HostileSubject = "<a href=\"https://phish.example\">Verify</a><script>steal()</script>";

    /// <summary>The branding the renderer under test reads from, unless overridden per-call.</summary>
    private readonly FakeEmailBrandingProvider _branding = new();

    /// <summary>The brand every test starts from.</summary>
    private static readonly EmailBranding Acme = new(
        SiteName: "Acme Hosting",
        LogoUrl: "https://acme.test/uploads/branding/logodark/x/logo.png",
        Primary: "#1a73e8",
        PrimaryDark: "#154485",
        OnPrimary: "#ffffff",
        Accent: "#e8710a");

    /// <summary>Builds the renderer under test, over the given brand.</summary>
    /// <param name="brand">The brand the fake provider answers with, defaulting to <see cref="Acme"/>.</param>
    /// <returns>The renderer.</returns>
    private TemplateRenderer CreateRenderer(EmailBranding? brand = null)
    {
        _branding.BrandToReturn = brand ?? Acme;
        return new TemplateRenderer(_branding);
    }

    /// <summary>A model member is read under its lower snake case name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderHtmlAsync_ModelMember_RendersUnderItsSnakeCaseName()
    {
        var html = await CreateRenderer().RenderHtmlAsync(
            "Reset: {{ reset_link }}", new PasswordResetTemplateModel("https://x/reset?t=1"), CancellationToken.None);

        Assert.Equal("Reset: https://x/reset?t=1", html);
    }

    /// <summary>The members of a nested record render too, which a registration-based strategy would leave blank.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderTextAsync_NestedRecord_RendersItsMembers()
    {
        var model = new RenewalReminderTemplateModel(
            new RenewalInvoiceTemplateData(42, 19.90m, "USD", "2026-11-01", "https://x/client/invoices/42"));

        var text = await CreateRenderer().RenderTextAsync(
            "#{{ invoice.id }} {{ invoice.total }} {{ invoice.currency }} {{ invoice.due_date }} {{ invoice.url }}",
            model,
            CancellationToken.None);

        Assert.Equal("#42 19.90 USD 2026-11-01 https://x/client/invoices/42", text);
    }

    /// <summary>Every field of the brand and the site name are available beside the model.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderTextAsync_ExposesBrandAndSiteBesideTheModel()
    {
        var text = await CreateRenderer().RenderTextAsync(
            "{{ site.name }}|{{ brand.primary }}|{{ brand.primary_dark }}|{{ brand.on_primary }}|{{ brand.accent }}|{{ brand.logo_url }}|{{ reset_link }}",
            new PasswordResetTemplateModel("L"),
            CancellationToken.None);

        Assert.Equal("Acme Hosting|#1a73e8|#154485|#ffffff|#e8710a|https://acme.test/uploads/branding/logodark/x/logo.png|L", text);
    }

    /// <summary>A logo-or-site-name branch in a seeded layout template follows whichever brand is injected.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderHtmlAsync_LogoBranchInTheSeededLayout_FollowsTheBrand()
    {
        const string template = "{% if brand.logo_url != \"\" %}<img src=\"{{ brand.logo_url }}\">{% else %}<span>{{ site.name }}</span>{% endif %}";
        var model = new PasswordResetTemplateModel("L");

        var withLogo = await CreateRenderer().RenderHtmlAsync(template, model, CancellationToken.None);
        var without = await CreateRenderer(Acme with { LogoUrl = "" }).RenderHtmlAsync(template, model, CancellationToken.None);

        Assert.Contains("<img src=\"https://acme.test", withLogo);
        Assert.Equal("<span>Acme Hosting</span>", without);
    }

    /// <summary>A body encodes a hostile ticket subject, so no link or script reaches the mail as markup.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderHtmlAsync_HostileTicketSubject_IsEncoded()
    {
        var model = new TicketClosedTemplateModel(new TicketTemplateData(7, HostileSubject));

        var html = await CreateRenderer().RenderHtmlAsync("<p>{{ ticket.subject }}</p>", model, CancellationToken.None);

        Assert.Equal(
            "<p>&lt;a href=&quot;https://phish.example&quot;&gt;Verify&lt;/a&gt;&lt;script&gt;steal()&lt;/script&gt;</p>",
            html);
    }

    /// <summary>A body keeps Russian and Armenian text readable while still encoding the markup characters.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderHtmlAsync_CyrillicAndArmenianText_StaysReadableWhileMarkupIsEncoded()
    {
        var model = new TicketClosedTemplateModel(new TicketTemplateData(7, "Привет, Բարև <b> & \"q\""));

        var html = await CreateRenderer().RenderHtmlAsync("<p>{{ ticket.subject }}</p>", model, CancellationToken.None);

        Assert.Equal("<p>Привет, Բարև &lt;b&gt; &amp; &quot;q&quot;</p>", html);
    }

    /// <summary>A subject is a plain-text header, so the same value prints as it is, not as entities.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderTextAsync_HostileTicketSubject_PrintsAsTyped()
    {
        var model = new TicketRepliedTemplateModel(new TicketTemplateData(7, HostileSubject));

        var text = await CreateRenderer().RenderTextAsync("#{{ ticket.id }}: {{ ticket.subject }}", model, CancellationToken.None);

        Assert.Equal($"#7: {HostileSubject}", text);
    }

    /// <summary>
    /// <c>brand</c> and <c>site</c> belong to the renderer: no template model declares a member that
    /// would read as either, at any depth a template can reach.
    /// </summary>
    [Fact]
    public void TemplateModels_DoNotDeclareTheReservedNames()
    {
        var reserved = new[] { "Brand", "Site" };
        var offenders = typeof(EmailTemplateModel).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(EmailTemplateModel).Namespace)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => reserved.Contains(p.Name))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>A template the parser cannot read throws rather than rendering partial output.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenderHtmlAsync_ParseError_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRenderer().RenderHtmlAsync("{{ unclosed", new PasswordResetTemplateModel("L"), CancellationToken.None));
    }
}
