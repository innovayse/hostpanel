using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Faqs;
using Innovayse.Hostpanel.Domain.Faqs.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AuditableEntityInterceptor</c> against Postgres: <c>CreatedAt</c> is stamped from the clock
/// on insert, survives later updates, and a creation date carried over from history is kept.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AuditableEntityInterceptorTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The moment the test clock starts at; whole seconds so Postgres stores it exactly.</summary>
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a host of its own whose time source is <paramref name="clock"/>.</summary>
    /// <param name="clock">The hand-moved time source the host will read.</param>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> HostWithClock(ManualTimeProvider clock) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));

    /// <summary>Inserting stamps the clock's moment; a later update leaves it as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Save_StampsCreatedAtOnInsert_AndKeepsItOnUpdateAsync()
    {
        var clock = new ManualTimeProvider(Start);
        var host = HostWithClock(clock);
        int id;

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var faq = Faq.Create(FaqCategory.General, sortOrder: 1, isActive: true);
            context.Faqs.Add(faq);
            await context.SaveChangesAsync(CancellationToken.None);

            faq.CreatedAt.Should().Be(Start);
            id = faq.Id;
        }

        clock.Advance(TimeSpan.FromHours(2));

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var faq = await context.Faqs.SingleAsync(f => f.Id == id);
            faq.Update(FaqCategory.Products, sortOrder: 5, isActive: false);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var stored = await context.Faqs.AsNoTracking().SingleAsync(f => f.Id == id);

            stored.SortOrder.Should().Be(5);
            stored.CreatedAt.Should().Be(Start);
        }
    }

    /// <summary>A creation date set before the first save, as the history import does, is not overwritten.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Save_KeepsABackdatedCreatedAtOnInsertAsync()
    {
        var host = HostWithClock(new ManualTimeProvider(Start));
        var written = Start.AddYears(-3);
        int id;

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var announcement = Announcement.Create($"Title-{Guid.NewGuid():N}", "Body", isPublished: true);
            announcement.Backdate(written);
            context.Announcements.Add(announcement);
            await context.SaveChangesAsync(CancellationToken.None);
            id = announcement.Id;
        }

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var stored = await context.Announcements.AsNoTracking().SingleAsync(a => a.Id == id);

            stored.CreatedAt.Should().Be(written);
        }
    }
}
