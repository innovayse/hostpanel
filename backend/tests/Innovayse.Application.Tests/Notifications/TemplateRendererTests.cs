namespace Innovayse.Application.Tests.Notifications;

using Innovayse.Application.Notifications.Common;
using Innovayse.Application.Notifications.Interfaces;
using Innovayse.Application.Notifications.Services;
using Moq;
using Xunit;

/// <summary>
/// Tests for <see cref="TemplateRenderer"/>: the caller's model renders as before, the brand
/// and site objects are there for every template, and the caller's own member wins on a clash.
/// </summary>
public class TemplateRendererTests
{
    private readonly Mock<IEmailBrandingProvider> branding = new();

    private static readonly EmailBranding Acme = new(
        SiteName: "Acme Hosting",
        LogoUrl: "https://acme.test/uploads/branding/logodark/x/logo.png",
        Primary: "#1a73e8",
        PrimaryDark: "#154485",
        OnPrimary: "#ffffff",
        Accent: "#e8710a");

    private TemplateRenderer CreateRenderer(EmailBranding? brand = null)
    {
        branding.Setup(b => b.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(brand ?? Acme);
        return new TemplateRenderer(branding.Object);
    }

    [Fact]
    public async Task RenderAsync_CallerModel_RendersAsBefore()
    {
        var html = await CreateRenderer().RenderAsync("Reset: {{ reset_link }}", new { reset_link = "https://x/reset?t=1" });

        Assert.Equal("Reset: https://x/reset?t=1", html);
    }

    [Fact]
    public async Task RenderAsync_ExposesBrandAndSiteBesideTheModel()
    {
        var html = await CreateRenderer().RenderAsync(
            "{{ site.name }}|{{ brand.primary }}|{{ brand.primary_dark }}|{{ brand.on_primary }}|{{ brand.accent }}|{{ brand.logo_url }}|{{ reset_link }}",
            new { reset_link = "L" });

        Assert.Equal("Acme Hosting|#1a73e8|#154485|#ffffff|#e8710a|https://acme.test/uploads/branding/logodark/x/logo.png|L", html);
    }

    [Fact]
    public async Task RenderAsync_LogoBranchInTheSeededLayout_FollowsTheBrand()
    {
        const string template = "{% if brand.logo_url != \"\" %}<img src=\"{{ brand.logo_url }}\">{% else %}<span>{{ site.name }}</span>{% endif %}";

        var withLogo = await CreateRenderer().RenderAsync(template, new { });
        var without = await CreateRenderer(Acme with { LogoUrl = "" }).RenderAsync(template, new { });

        Assert.Contains("<img src=\"https://acme.test", withLogo);
        Assert.Equal("<span>Acme Hosting</span>", without);
    }

    [Fact]
    public async Task RenderAsync_CallerOwnBrandMember_WinsOverTheInjectedOne()
    {
        var html = await CreateRenderer().RenderAsync(
            "{{ brand }}|{{ site.name }}",
            new { brand = "caller's brand" });

        Assert.Equal("caller's brand|Acme Hosting", html);
    }

    [Fact]
    public async Task RenderAsync_DictionaryModel_KeepsItsOwnSiteKey()
    {
        var model = new Dictionary<string, object> { ["site"] = "caller's site", ["x"] = 1 };

        var html = await CreateRenderer().RenderAsync("{{ site }}|{{ brand.primary }}", model);

        Assert.Equal("caller's site|#1a73e8", html);
    }

    [Fact]
    public async Task RenderAsync_DifferentlyCasedKey_DoesNotSuppressTheInjectedObject()
    {
        // Fluid resolves names exactly, so "Site" is not {{ site }} and the brand's site must
        // still be there.
        var model = new Dictionary<string, object> { ["Site"] = "caller's site" };

        var html = await CreateRenderer().RenderAsync("{{ site.name }}", model);

        Assert.Equal("Acme Hosting", html);
    }

    [Fact]
    public async Task RenderAsync_ParseError_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRenderer().RenderAsync("{{ unclosed", new { }));
    }
}
