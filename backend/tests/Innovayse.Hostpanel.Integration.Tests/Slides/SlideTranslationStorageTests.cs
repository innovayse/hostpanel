using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Slides.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for how <see cref="SlideTranslation.Features"/> is stored: the text column keeps
/// the formats rows were written in while the property was a raw string, and each reads back safely.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SlideTranslationStorageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A JSON array stored with non-ASCII letters escaped, as the default serializer writes it,
    /// reads back as the same bullet points.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LegacyEscapedJson_ReadsBackAsTheBulletPointsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slideId = await SeedSlideAsync(context);

        await SetStoredFeaturesAsync(context, slideId, """["Free SSL","Արագ"]""");

        var translation = await ReadTranslationAsync(context, slideId);
        Assert.NotNull(translation.Features);
        Assert.Equal(["Free SSL", "Արագ"], translation.Features);
    }

    /// <summary>
    /// Text that is not a JSON array of strings reads back as no bullet points, as the readers before
    /// the converter did, instead of failing the query.
    /// </summary>
    /// <param name="stored">The column text as it sits in the table.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Free SSL, Daily backups")]
    [InlineData("")]
    [InlineData("{\"a\":1}")]
    public async Task NonJsonText_ReadsBackAsNoBulletPointsAsync(string stored)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slideId = await SeedSlideAsync(context);

        await SetStoredFeaturesAsync(context, slideId, stored);

        var translation = await ReadTranslationAsync(context, slideId);
        Assert.Null(translation.Features);
    }

    /// <summary>Seeds a slide with one English translation carrying bullet points.</summary>
    /// <param name="context">Data context.</param>
    /// <returns>The new slide's id.</returns>
    private static async Task<int> SeedSlideAsync(AppDbContext context)
    {
        var slide = Slide.Create(
            new SlideSettings("mdi-server", "#123456", "/img/slide.png", null, null, null, 0, true, SlideAudience.All, null, null),
            DateTimeOffset.UtcNow);
        slide.SetTranslation("en", "Storage test", null, null, ["placeholder"], null, null);
        context.Slides.Add(slide);
        await context.SaveChangesAsync(CancellationToken.None);
        return slide.Id;
    }

    /// <summary>Overwrites the stored bullet-point column with raw text, bypassing the converter.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="slideId">The slide whose translation is changed.</param>
    /// <param name="stored">The text to store.</param>
    /// <returns>A task that completes once the row is updated.</returns>
    private static async Task SetStoredFeaturesAsync(AppDbContext context, int slideId, string stored)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "SlideTranslations" SET "Features" = {stored} WHERE "SlideId" = {slideId}""",
            CancellationToken.None);
    }

    /// <summary>Reads the slide's translation back through EF Core, untracked.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="slideId">The slide whose translation is read.</param>
    /// <returns>The translation as the converter reads it.</returns>
    private static async Task<SlideTranslation> ReadTranslationAsync(AppDbContext context, int slideId)
    {
        return await context.SlideTranslations
            .AsNoTracking()
            .SingleAsync(t => t.SlideId == slideId, CancellationToken.None);
    }
}
