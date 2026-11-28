using System.Globalization;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetService;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Extensions;

/// <summary>
/// Sends the three free trial emails (<c>trial-ending</c>, <c>trial-converted</c>, <c>trial-ended</c>) the same way:
/// the service and the client are looked up, the templates are seeded, and the mail goes out in the client's
/// language with the trial's common data.
/// </summary>
public static class TrialEmailExtensions
{
    /// <summary>
    /// Mails the client about their trial. Nothing is sent when the service is gone or the client has no email address.
    /// </summary>
    /// <param name="bus">Resolves the service and the client and sends the mail.</param>
    /// <param name="context">The module's data context, to seed the templates.</param>
    /// <param name="clientPortal">Where the client area lives, for the service link.</param>
    /// <param name="evt">The trial event, which keys the mail so a replay sends it once.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="trialEndsAt">When the trial ends (or ended), UTC.</param>
    /// <param name="templateSlug">The template to render.</param>
    /// <param name="model">Builds the template's data around the trial's common data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the mail is handed over, or nothing was sent.</returns>
    public static async Task SendTrialEmailAsync(
        this IMessageBus bus,
        IHostpanelDbContext context,
        ClientPortalOptions clientPortal,
        IDomainEvent evt,
        int serviceId,
        int clientId,
        DateTimeOffset trialEndsAt,
        string templateSlug,
        Func<TrialTemplateData, EmailTemplateModel> model,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clientPortal);
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(model);

        var service = (await bus.InvokeAsync<Result<ServiceDetailDto>>(new GetServiceQuery(serviceId), ct)).Value;
        if (service is null)
        {
            return;
        }

        var client = await bus.FindClientBriefAsync(clientId, withEmail: true, ct);
        if (client is not { Email: { } email } || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await TrialTemplatesSeeder.EnsureSeededAsync(context, ct);
        var trial = new TrialTemplateData(
            serviceId,
            service.ProductName,
            trialEndsAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            $"{clientPortal.BaseUrl.TrimEnd('/')}/client/services/{serviceId}");

        await bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                templateSlug,
                model(trial),
                new ContactCopy(clientId, ContactNotification.Product),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, templateSlug)),
            ct);
    }
}
