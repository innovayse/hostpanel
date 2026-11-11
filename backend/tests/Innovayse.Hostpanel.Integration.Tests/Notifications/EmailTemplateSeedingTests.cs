using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the seeding of email template language versions: every language is added,
/// a version already stored is never overwritten, and two seeders racing for the same versions
/// both finish and store each version once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EmailTemplateSeedingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The seeded password reset mail exists in English, Russian and Armenian.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PasswordReset_IsSeededInEveryLanguage()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await PasswordResetTemplateSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var languages = await context.EmailTemplates.AsNoTracking()
            .Where(t => t.Slug == PasswordResetTemplateSeeder.Slug)
            .Select(t => t.Language)
            .ToListAsync();
        languages.Should().BeEquivalentTo(["en", "ru", "hy"]);
    }

    /// <summary>A version an operator edited keeps the edit when the seed runs again; only the missing ones are added.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddMissing_KeepsAStoredVersionAndAddsTheRest()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"seed-{Guid.NewGuid():N}";
        context.EmailTemplates.Add(EmailTemplate.Create(slug, "ru", "Edited by staff", "Edited body", null));
        await context.SaveChangesAsync();

        await EmailTemplateSeeding.AddMissingAsync(
            context,
            [
                EmailTemplate.Create(slug, "en", "Seeded", "Body", null),
                EmailTemplate.Create(slug, "ru", "Seeded", "Body", null),
            ],
            CancellationToken.None);

        var stored = await context.EmailTemplates.AsNoTracking().Where(t => t.Slug == slug).ToListAsync();
        stored.Should().HaveCount(2);
        stored.Single(t => t.Language == "ru").Subject.Should().Be("Edited by staff");
        stored.Single(t => t.Language == "en").Subject.Should().Be("Seeded");
    }

    /// <summary>
    /// Two scopes that find the same slug and languages missing at once both finish without an
    /// error, and each slug and language pair is stored exactly once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddMissing_TwoSeedersAtOnce_BothSucceedAndStoreEachVersionOnce()
    {
        var slug = $"seed-race-{Guid.NewGuid():N}";
        var rendezvous = new TemplateSeedRendezvousInterceptor(slug, parties: 2);
        await using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(rendezvous));
        }));
        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();

        var seeding = async () => await Task.WhenAll(SeedAsync(first, slug), SeedAsync(second, slug));

        await seeding.Should().NotThrowAsync();
        rendezvous.Arrived.Should().Be(2, "both seeders found the versions missing and tried to insert them");
        using var verify = host.Services.CreateScope();
        var languages = await verify.ServiceProvider.GetRequiredService<AppDbContext>().EmailTemplates.AsNoTracking()
            .Where(t => t.Slug == slug)
            .Select(t => t.Language)
            .ToListAsync();
        languages.Should().HaveCount(3).And.OnlyHaveUniqueItems().And.BeEquivalentTo(["en", "ru", "hy"]);
    }

    /// <summary>Seeds the English, Russian and Armenian versions of a slug through the scope's own data context.</summary>
    /// <param name="scope">The scope whose data context seeds.</param>
    /// <param name="slug">The template slug.</param>
    /// <returns>A task that completes once the seeder is done.</returns>
    private static Task SeedAsync(IServiceScope scope, string slug)
    {
        return EmailTemplateSeeding.AddMissingAsync(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            [
                EmailTemplate.Create(slug, "en", "Seeded", "Body", null),
                EmailTemplate.Create(slug, "ru", "Seeded", "Body", null),
                EmailTemplate.Create(slug, "hy", "Seeded", "Body", null),
            ],
            CancellationToken.None);
    }
}
