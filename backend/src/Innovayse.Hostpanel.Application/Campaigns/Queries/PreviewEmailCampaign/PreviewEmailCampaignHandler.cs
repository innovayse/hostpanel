using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Queries.FindEmailTemplate;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.PreviewEmailCampaign;

/// <summary>
/// Renders the stored campaign template with the campaign's subject and body and a sample unsubscribe link, in the default
/// language, so the admin sees the frame a recipient gets without anything being sent.
/// </summary>
public sealed class PreviewEmailCampaignHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the campaign template is looked up through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Renders the template against the campaign's data, as a real send does.</summary>
    private readonly TemplateRenderer _renderer;

    /// <summary>The client portal's address, which the sample unsubscribe link points at.</summary>
    private readonly IOptions<ClientPortalOptions> _portal;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the campaign template is looked up through.</param>
    /// <param name="renderer">Renders the template against the campaign's data.</param>
    /// <param name="portal">The client portal's address.</param>
    public PreviewEmailCampaignHandler(
        IHostpanelDbContext context, IMessageBus bus, TemplateRenderer renderer, IOptions<ClientPortalOptions> portal)
    {
        _context = context;
        _bus = bus;
        _renderer = renderer;
        _portal = portal;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The campaign to render.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rendered subject and HTML, <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign, or <c>EMAIL_TEMPLATE_NOT_FOUND</c> when the campaign template was deleted.</returns>
    public async Task<Result<EmailCampaignPreviewDto>> HandleAsync(PreviewEmailCampaignQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaign = await _context.EmailCampaigns.AsNoTracking().IgnoreAutoIncludes().FirstOrDefaultAsync(x => x.Id == query.Id, ct);
        if (campaign is null)
        {
            return Result<EmailCampaignPreviewDto>.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        var found = await _bus.InvokeAsync<Result<EmailTemplateDto>>(new FindEmailTemplateQuery(EmailCampaignTemplateSeeder.Slug, null), ct);
        if (found.Value is not { } template)
        {
            return Result<EmailCampaignPreviewDto>.FailureCode(HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
        }

        var model = new EmailCampaignTemplateModel(
            campaign.Subject, campaign.Body, CampaignUnsubscribeLink.For(_portal.Value.BaseUrl, CampaignUnsubscribeLink.SampleToken));
        var subject = await _renderer.RenderTextAsync(template.Subject, model, ct);
        var html = await _renderer.RenderHtmlAsync(template.Body, model, ct);

        return Result<EmailCampaignPreviewDto>.Success(new EmailCampaignPreviewDto(subject, html));
    }
}
