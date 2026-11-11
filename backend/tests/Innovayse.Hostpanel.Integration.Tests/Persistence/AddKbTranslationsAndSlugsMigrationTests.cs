using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The <c>AddKbTranslationsAndSlugs</c> migration on data saved before it: articles that named their category as text
/// move to category rows, get slugs that cannot clash, and keep their text as a translation in the language it is
/// written in. The migration is applied, rolled back and applied again, and the constraints it adds are exercised.
/// </summary>
public sealed class AddKbTranslationsAndSlugsMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261108040919_AddDomainLineRenewals";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261109150024_AddKbTranslationsAndSlugs";

    /// <summary>The article titles seeded to make slugs clash, in id order. Their slugs are checked one by one.</summary>
    private static readonly string[] ClashingTitles = ["Foo", "Foo", "Foo-2", "FOO", "foo 2"];

    /// <summary>Applying the migration moves every article to a category row, a slug and a translation, and loses none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Up_OnArticlesSavedBeforeIt_MovesThemToCategoriesSlugsAndTranslationsAsync()
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, PreviousMigration);
            var seeded = await SeedLegacyDataAsync(connectionString);
            var publishedBefore = await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticles" WHERE "IsPublished" """);

            await MigrateAsync(options, TargetMigration);

            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticles" """)).Should().Be(seeded.ArticleIds.Count, "no article is lost");
            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticles" WHERE "IsPublished" """)).Should().Be(publishedBefore, "no published article is lost");
            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticleTranslations" """)).Should().Be(seeded.ArticleIds.Count, "one translation each");

            // Categories: the two Hosting rows stay, case variants of Billing become one new category, and a blank text is General.
            var categories = await RowsAsync(connectionString, """SELECT "Id", "Name", "Slug" FROM "KbCategories" ORDER BY "Id" """);
            categories.Select(c => (string)c["Name"]!).Should().Contain(["Hosting", "HOSTING", "billing", "General"]);
            categories.Count(c => string.Equals((string)c["Name"]!, "billing", StringComparison.OrdinalIgnoreCase)).Should().Be(1, "spellings that differ by case or edge spaces are one category");
            (await ScalarAsync<string>(connectionString, """SELECT "Name" FROM "KbCategories" WHERE lower("Name") = 'billing' """)).Should().Be("billing", "the most used spelling names it: it is used twice, the others once");

            var hostingId = (int)categories.First(c => (string)c["Name"]! == "Hosting")["Id"]!;
            (await ScalarAsync<int>(connectionString, $"""SELECT "CategoryId" FROM "KbArticles" WHERE "Id" = {seeded.HostingArticleId}""")).Should().Be(hostingId, "an article joins the lowest-id category that matches its text");
            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticles" a LEFT JOIN "KbCategories" c ON c."Id" = a."CategoryId" WHERE c."Id" IS NULL""")).Should().Be(0);

            // Slugs: unique without regard to case, never numeric, never a reserved word, whatever the titles were.
            var slugs = (await RowsAsync(connectionString, """SELECT "Slug" FROM "KbArticles" ORDER BY "Id" """)).Select(r => (string)r["Slug"]!).ToList();
            slugs.Should().OnlyContain(s => System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-z0-9]+(-[a-z0-9]+)*$"));
            slugs.Select(s => s.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
            slugs.Should().NotContain(["articles", "categories"]).And.NotContain(s => s.All(char.IsAsciiDigit));
            categories.Select(c => (string)c["Slug"]!).Should().OnlyHaveUniqueItems();
            var categorySlugs = categories.ToDictionary(c => (string)c["Name"]!, c => (string)c["Slug"]!);
            categorySlugs["Hosting"].Should().Be("hosting");
            categorySlugs["HOSTING"].Should().Be("hosting-2", "the second name that builds the same slug is numbered");
            (categorySlugs["billing"], categorySlugs["General"], categorySlugs["Docs"]).Should().Be(("billing", "general", "docs"));

            var clashing = await SlugsOfAsync(connectionString, seeded.ClashingIds);
            clashing.Should().Equal("foo", "foo-2", "foo-2-2", "foo-3", "foo-2-3");

            (await SlugOfAsync(connectionString, seeded.NumericId)).Should().Be("article-2024");
            (await SlugOfAsync(connectionString, seeded.ReservedId)).Should().Be("articles-2");
            (await SlugOfAsync(connectionString, seeded.AccentedId)).Should().Be("cafe-creme");
            (await SlugOfAsync(connectionString, seeded.CyrillicId)).Should().Be($"article-{seeded.CyrillicId}", "a title with no Latin letters is named by its id");
            (await SlugOfAsync(connectionString, seeded.EmptyId)).Should().Be($"article-{seeded.EmptyId}");

            // Language: Armenian letters mean hy, otherwise Cyrillic means ru, otherwise en. Title and body are kept as they were.
            (await LocaleOfAsync(connectionString, seeded.CyrillicId)).Should().Be("ru");
            (await LocaleOfAsync(connectionString, seeded.ArmenianId)).Should().Be("hy");
            (await LocaleOfAsync(connectionString, seeded.EnglishId)).Should().Be("en");
            (await LocaleOfAsync(connectionString, seeded.LatinTitleCyrillicBodyId)).Should().Be("ru");
            (await LocaleOfAsync(connectionString, seeded.ArmenianAndCyrillicId)).Should().Be("hy", "Armenian letters win over Cyrillic");
            (await ScalarAsync<string>(connectionString, $"""SELECT "Content" FROM "KbArticleTranslations" WHERE "ArticleId" = {seeded.EnglishId}""")).Should().Be("An English body.");
        });
    }

    /// <summary>Rolling back restores the old columns from the translations and the category names, and loses no article.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Down_AfterUp_RestoresTheOldColumnsAndLosesNoArticleAsync()
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, PreviousMigration);
            var seeded = await SeedLegacyDataAsync(connectionString);
            var publishedBefore = await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticles" WHERE "IsPublished" """);
            await MigrateAsync(options, TargetMigration);

            // A second language is added after the migration: going down keeps the English text, not this one.
            await ExecuteAsync(connectionString, $"""INSERT INTO "KbArticleTranslations" ("ArticleId", "Locale", "Title", "Content") VALUES ({seeded.EnglishId}, 'hy', 'Վերնագիր', 'Տեքստ') """);

            await MigrateAsync(options, PreviousMigration);

            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticles" """)).Should().Be(seeded.ArticleIds.Count);
            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticles" WHERE "IsPublished" """)).Should().Be(publishedBefore);
            (await ScalarAsync<string>(connectionString, $"""SELECT "Title" FROM "KbArticles" WHERE "Id" = {seeded.EnglishId}""")).Should().Be("English title");
            (await ScalarAsync<string>(connectionString, $"""SELECT "Content" FROM "KbArticles" WHERE "Id" = {seeded.EnglishId}""")).Should().Be("An English body.");
            (await ScalarAsync<string>(connectionString, $"""SELECT "Title" FROM "KbArticles" WHERE "Id" = {seeded.CyrillicId}""")).Should().Be("Привет", "an article with one language gets that language back");
            (await ScalarAsync<string>(connectionString, $"""SELECT "Category" FROM "KbArticles" WHERE "Id" = {seeded.BillingLowerId}""")).Should().Be("billing", "the category comes back as its name: the original spelling of a merged variant is not kept");
            (await ScalarAsync<string>(connectionString, $"""SELECT "Category" FROM "KbArticles" WHERE "Id" = {seeded.EmptyId}""")).Should().Be("General");
            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM information_schema.tables WHERE table_name = 'KbArticleTranslations' """)).Should().Be(0);
            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM information_schema.columns WHERE table_name = 'KbArticles' AND column_name IN ('Slug', 'CategoryId') """)).Should().Be(0);
        });
    }

    /// <summary>Applying it again after a rollback gives the same slugs, categories and translations, and creates no category twice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpDownUp_GivesTheSameStateTwiceAsync()
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, PreviousMigration);
            await SeedLegacyDataAsync(connectionString);

            await MigrateAsync(options, TargetMigration);
            var first = await StateAsync(connectionString);
            await MigrateAsync(options, PreviousMigration);
            await MigrateAsync(options, TargetMigration);
            var second = await StateAsync(connectionString);

            second.Should().Equal(first);
        });
    }

    /// <summary>The slug SQL builds for a title is the one the application's <see cref="SlugGenerator"/> builds, accents and all.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Up_BuildsTheSameSlugAsTheApplicationsSlugGeneratorAsync()
    {
        string[] titles = ["Doğru", "Café Crème", "Straße", "Łódź Ørsted", "Hello, World!", "  --Trim--  ", "Ünïcödé Tëst", "Ångström Æble"];
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, PreviousMigration);
            var ids = new List<int>();
            foreach (var title in titles)
            {
                ids.Add(await InsertAsync(
                    connectionString,
                    """INSERT INTO "KbArticles" ("Title", "Content", "Category", "IsPublished") VALUES (@t, 'Body.', 'Docs', true) RETURNING "Id" """,
                    ("t", title)));
            }

            await MigrateAsync(options, TargetMigration);

            (await SlugsOfAsync(connectionString, ids))
                .Should().Equal(titles.Select(title => SlugGenerator.FromText(title, "article", 140)));
        });
    }

    /// <summary>A long numeric title is cut first and then found to be numeric, so it gets the prefix and never ends up a bare number.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Up_CutsALongNumericTitleBeforeCheckingWhetherItIsNumericAsync()
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, PreviousMigration);
            var id = await InsertAsync(
                connectionString,
                """INSERT INTO "KbArticles" ("Title", "Content", "Category", "IsPublished") VALUES (@t, 'Body.', 'Docs', true) RETURNING "Id" """,
                ("t", new string('1', 150)));

            await MigrateAsync(options, TargetMigration);

            (await SlugOfAsync(connectionString, id)).Should().Be("article-" + new string('1', 140));
        });
    }

    /// <summary>Going down with two non English translations takes the first of them in order, here Armenian before Russian.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Down_WithTwoNonEnglishTranslations_TakesTheFirstInOrderAsync()
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, PreviousMigration);
            var id = await InsertAsync(
                connectionString,
                """INSERT INTO "KbArticles" ("Title", "Content", "Category", "IsPublished") VALUES ('Plain', 'Plain body.', 'Docs', true) RETURNING "Id" """);
            await MigrateAsync(options, TargetMigration);
            await ExecuteAsync(connectionString, $"""DELETE FROM "KbArticleTranslations" WHERE "ArticleId" = {id}""");
            await ExecuteAsync(connectionString, $"""INSERT INTO "KbArticleTranslations" ("ArticleId", "Locale", "Title", "Content") VALUES ({id}, 'ru', 'Русский', 'Русский текст.') """);
            await ExecuteAsync(connectionString, $"""INSERT INTO "KbArticleTranslations" ("ArticleId", "Locale", "Title", "Content") VALUES ({id}, 'hy', 'Հայերեն', 'Հայերեն տեքստ.') """);

            await MigrateAsync(options, PreviousMigration);

            (await ScalarAsync<string>(connectionString, $"""SELECT "Title" FROM "KbArticles" WHERE "Id" = {id}""")).Should().Be("Հայերեն");
        });
    }

    /// <summary>The check constraints and the unique index refuse a slug the rules do not allow.</summary>
    /// <param name="slug">The slug an insert tries.</param>
    /// <param name="sqlState">The Postgres error code expected.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Upper", "23514")]
    [InlineData("12345", "23514")]
    [InlineData("articles", "23514")]
    [InlineData("categories", "23514")]
    [InlineData("two  spaces", "23514")]
    [InlineData("-lead", "23514")]
    [InlineData("under_score", "23514")]
    [InlineData("foo\n", "23514")]
    [InlineData("foo\r\n", "23514")]
    [InlineData("taken", "23505")]
    public async Task ArticleSlugConstraints_RefuseWhatTheRulesForbidAsync(string slug, string sqlState)
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, TargetMigration);
            var categoryId = await InsertAsync(connectionString, """INSERT INTO "KbCategories" ("Name", "Slug", "Description", "IsHidden") VALUES ('Cat', 'cat', '', false) RETURNING "Id" """);
            await ExecuteAsync(connectionString, $"""INSERT INTO "KbArticles" ("CategoryId", "Slug", "IsPublished") VALUES ({categoryId}, 'taken', false) """);

            var act = () => ExecuteAsync(connectionString, $"""INSERT INTO "KbArticles" ("CategoryId", "Slug", "IsPublished") VALUES ({categoryId}, '{slug}', false) """);

            (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(sqlState);
        });
    }

    /// <summary>A category slug follows the same shape rules but may be a word the article routes reserve; a duplicate is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategorySlugConstraints_AllowReservedWords_ButRefuseNumbersAndDuplicatesAsync()
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, TargetMigration);

            await ExecuteAsync(connectionString, """INSERT INTO "KbCategories" ("Name", "Slug", "Description", "IsHidden") VALUES ('A', 'articles', '', false) """);
            var number = () => ExecuteAsync(connectionString, """INSERT INTO "KbCategories" ("Name", "Slug", "Description", "IsHidden") VALUES ('B', '42', '', false) """);
            var duplicate = () => ExecuteAsync(connectionString, """INSERT INTO "KbCategories" ("Name", "Slug", "Description", "IsHidden") VALUES ('C', 'articles', '', false) """);

            (await number.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
            (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");
        });
    }

    /// <summary>A category that holds an article cannot be deleted in the database, and deleting an article takes its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForeignKeys_ProtectACategoryWithArticles_AndCascadeTranslationsAsync()
    {
        await WithDatabaseAsync(async (connectionString, options) =>
        {
            await MigrateAsync(options, TargetMigration);
            var categoryId = await InsertAsync(connectionString, """INSERT INTO "KbCategories" ("Name", "Slug", "Description", "IsHidden") VALUES ('Cat', 'cat', '', false) RETURNING "Id" """);
            var articleId = await InsertAsync(connectionString, $"""INSERT INTO "KbArticles" ("CategoryId", "Slug", "IsPublished") VALUES ({categoryId}, 'one', true) RETURNING "Id" """);
            await ExecuteAsync(connectionString, $"""INSERT INTO "KbArticleTranslations" ("ArticleId", "Locale", "Title", "Content") VALUES ({articleId}, 'en', 'T', 'C') """);

            var deleteCategory = () => ExecuteAsync(connectionString, $"""DELETE FROM "KbCategories" WHERE "Id" = {categoryId}""");
            var missingCategory = () => ExecuteAsync(connectionString, """INSERT INTO "KbArticles" ("CategoryId", "Slug", "IsPublished") VALUES (987654321, 'orphan', false) """);

            (await deleteCategory.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23503");
            (await missingCategory.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23503");
            await ExecuteAsync(connectionString, $"""DELETE FROM "KbArticles" WHERE "Id" = {articleId}""");
            (await ScalarAsync<long>(connectionString, """SELECT count(*) FROM "KbArticleTranslations" """)).Should().Be(0);
        });
    }

    /// <summary>What the legacy seed created, for the assertions.</summary>
    /// <param name="ArticleIds">Every article id.</param>
    /// <param name="ClashingIds">The ids of the articles titled by <see cref="ClashingTitles"/>, in order.</param>
    /// <param name="HostingArticleId">The article whose category text is a case variant of two Hosting rows.</param>
    /// <param name="BillingLowerId">An article whose category text is a lower case spelling of Billing.</param>
    /// <param name="NumericId">The article titled with a number.</param>
    /// <param name="ReservedId">The article titled with a reserved word.</param>
    /// <param name="AccentedId">The article with an accented Latin title.</param>
    /// <param name="CyrillicId">The article written in Russian.</param>
    /// <param name="ArmenianId">The article written in Armenian.</param>
    /// <param name="EnglishId">The article written in English.</param>
    /// <param name="EmptyId">The article with an empty title and a blank category.</param>
    /// <param name="LatinTitleCyrillicBodyId">The article with a Latin title and a Russian body.</param>
    /// <param name="ArmenianAndCyrillicId">The article whose text has both Armenian and Cyrillic letters.</param>
    private sealed record LegacySeed(
        IReadOnlyList<int> ArticleIds,
        IReadOnlyList<int> ClashingIds,
        int HostingArticleId,
        int BillingLowerId,
        int NumericId,
        int ReservedId,
        int AccentedId,
        int CyrillicId,
        int ArmenianId,
        int EnglishId,
        int EmptyId,
        int LatinTitleCyrillicBodyId,
        int ArmenianAndCyrillicId);

    /// <summary>Saves categories and articles the way the code before the migration did.</summary>
    /// <param name="connectionString">The database.</param>
    /// <returns>The ids the assertions need.</returns>
    private static async Task<LegacySeed> SeedLegacyDataAsync(string connectionString)
    {
        await InsertAsync(connectionString, """INSERT INTO "KbCategories" ("Name", "Description", "IsHidden") VALUES ('Hosting', 'd', false) RETURNING "Id" """);
        await InsertAsync(connectionString, """INSERT INTO "KbCategories" ("Name", "Description", "IsHidden") VALUES ('HOSTING', 'd', false) RETURNING "Id" """);

        var ids = new List<int>();

        // Saves one article the way the old code did and remembers its id.
        async Task<int> Article(string title, string content, string category, bool published)
        {
            var id = await InsertAsync(
                connectionString,
                """INSERT INTO "KbArticles" ("Title", "Content", "Category", "IsPublished") VALUES (@t, @c, @k, @p) RETURNING "Id" """,
                ("t", title), ("c", content), ("k", category), ("p", published));
            ids.Add(id);
            return id;
        }

        var clashing = new List<int>();
        for (var i = 0; i < ClashingTitles.Length; i++)
        {
            clashing.Add(await Article(ClashingTitles[i], "Body.", "Docs", i % 2 == 0));
        }

        var hosting = await Article("Plans", "About plans.", "hosting ", true);
        await Article("Billing one", "Pay.", "Billing", true);
        var billingLower = await Article("Billing two", "Pay.", "billing", false);
        await Article("Billing four", "Pay.", "billing", true);
        await Article("Billing three", "Pay.", " BILLING ", true);
        var numeric = await Article("2024", "A number.", "Docs", true);
        var reserved = await Article("Articles", "A word.", "Docs", false);
        var accented = await Article("Café Crème", "Accents.", "Docs", true);
        var cyrillic = await Article("Привет", "Русский текст.", "Docs", true);
        var armenian = await Article("Բարեւ", "Հայերեն տեքստ.", "Docs", false);
        var english = await Article("English title", "An English body.", "Docs", true);
        var empty = await Article(string.Empty, "No title.", string.Empty, false);
        var latinCyrillic = await Article("Setup guide", "Инструкция по настройке.", "Docs", true);
        var mixed = await Article("Tips", "Բարեւ Привет.", "Docs", true);

        return new LegacySeed(ids, clashing, hosting, billingLower, numeric, reserved, accented, cyrillic, armenian, english, empty, latinCyrillic, mixed);
    }

    /// <summary>Everything the migration decided, in a comparable form: slugs, categories and translations.</summary>
    /// <param name="connectionString">The database.</param>
    /// <returns>One line per fact, ordered.</returns>
    private static async Task<List<string>> StateAsync(string connectionString)
    {
        var lines = new List<string>();
        foreach (var row in await RowsAsync(connectionString, """SELECT "Id", "Name", "Slug" FROM "KbCategories" ORDER BY "Id" """))
        {
            lines.Add($"category {row["Id"]} {row["Name"]} {row["Slug"]}");
        }

        foreach (var row in await RowsAsync(connectionString, """SELECT "Id", "Slug", "CategoryId", "IsPublished" FROM "KbArticles" ORDER BY "Id" """))
        {
            lines.Add($"article {row["Id"]} {row["Slug"]} {row["CategoryId"]} {row["IsPublished"]}");
        }

        foreach (var row in await RowsAsync(connectionString, """SELECT "ArticleId", "Locale", "Title", "Content" FROM "KbArticleTranslations" ORDER BY "ArticleId", "Locale" """))
        {
            lines.Add($"translation {row["ArticleId"]} {row["Locale"]} {row["Title"]} {row["Content"]}");
        }

        return lines;
    }

    /// <summary>Runs a test against a fresh database and drops it afterwards.</summary>
    /// <param name="body">The test, given the connection string and the context options.</param>
    /// <returns>A task that completes when the test and the cleanup have run.</returns>
    private static async Task WithDatabaseAsync(Func<string, DbContextOptions<AppDbContext>, Task> body)
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await body(connectionString, new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Migrates the database up or down to a migration.</summary>
    /// <param name="options">The context options.</param>
    /// <param name="target">The migration to stop at.</param>
    /// <returns>A task that completes when the database is at the migration.</returns>
    private static async Task MigrateAsync(DbContextOptions<AppDbContext> options, string target)
    {
        await using var context = new AppDbContext(options, new ModelOnlyEncryptionService());
        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    /// <summary>The slug of an article.</summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="id">The article id.</param>
    /// <returns>The slug.</returns>
    private static Task<string> SlugOfAsync(string connectionString, int id) =>
        ScalarAsync<string>(connectionString, $"""SELECT "Slug" FROM "KbArticles" WHERE "Id" = {id}""");

    /// <summary>The slugs of several articles, in the order of the ids.</summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="ids">The article ids.</param>
    /// <returns>The slugs.</returns>
    private static async Task<List<string>> SlugsOfAsync(string connectionString, IEnumerable<int> ids)
    {
        var slugs = new List<string>();
        foreach (var id in ids)
        {
            slugs.Add(await SlugOfAsync(connectionString, id));
        }

        return slugs;
    }

    /// <summary>The locale of the only translation of an article.</summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="id">The article id.</param>
    /// <returns>The locale.</returns>
    private static Task<string> LocaleOfAsync(string connectionString, int id) =>
        ScalarAsync<string>(connectionString, $"""SELECT "Locale" FROM "KbArticleTranslations" WHERE "ArticleId" = {id}""");

    /// <summary>Inserts a row and returns the id it was given.</summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="sql">An <c>INSERT ... RETURNING "Id"</c>.</param>
    /// <param name="parameters">The named parameters.</param>
    /// <returns>The new id.</returns>
    private static Task<int> InsertAsync(string connectionString, string sql, params (string Name, object Value)[] parameters) =>
        ScalarAsync<int>(connectionString, sql, parameters);

    /// <summary>Runs a statement that returns nothing.</summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="sql">The statement.</param>
    /// <returns>A task that completes when it has run.</returns>
    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a query that returns one value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="connectionString">The database.</param>
    /// <param name="sql">The query.</param>
    /// <param name="parameters">The named parameters.</param>
    /// <returns>The first column of the first row.</returns>
    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Runs a query and reads every row as a dictionary of column to value.</summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="sql">The query.</param>
    /// <returns>One dictionary per row.</returns>
    private static async Task<List<Dictionary<string, object?>>> RowsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }
}
