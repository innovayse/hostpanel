using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Gives knowledge base articles a category by id, a slug and per-locale translations, and gives categories a slug.
    /// </summary>
    /// <remarks>
    /// The data is moved in SQL, in this order, inside the migration's transaction (a failure leaves the old schema):
    /// <list type="number">
    /// <item>categories named only by an article's text are created, matching by <c>lower(btrim(Name))</c>, so
    /// spellings that differ only by case or edge spaces are one category; the most used spelling names a new one;</item>
    /// <item>every article gets the lowest-id category that matches its text; a blank text means <c>General</c>;</item>
    /// <item>slugs are assigned one row at a time in id order, each checked against the slugs already given
    /// (the stored ones are lower case, so the check ignores case) and the words the article routes reserve, so a
    /// clash like <c>Foo</c>, <c>Foo</c>, <c>Foo-2</c> cannot leave a duplicate. One function builds the slug of a
    /// text, for categories and articles alike, and cuts it to 140 characters before it asks whether it is empty or numeric;</item>
    /// <item>each article's title and body become one translation, in the language its own text is written in: Armenian
    /// letters mean <c>hy</c>, otherwise Cyrillic means <c>ru</c>, otherwise <c>en</c>. The deployment's
    /// <c>DefaultLocale</c> lives in configuration, which SQL cannot read, so it is not guessed;</item>
    /// <item>only then are the slug, foreign key and check constraints added and the old columns dropped.</item>
    /// </list>
    /// <c>Down</c> is not exact: it restores <c>Category</c> as the category's name (the original spelling of a merged
    /// variant is gone), <c>Title</c> and <c>Content</c> from the <c>en</c> translation or else the first locale in
    /// order, and it loses the other languages and the slugs. The categories that <c>Up</c> created for article texts
    /// no category row named are kept: going down does not delete a row, and running <c>Up</c> again finds them by name.
    /// </remarks>
    public partial class AddKbTranslationsAndSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "KbCategories",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "KbArticles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "KbArticles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "KbArticleTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbArticleTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KbArticleTranslations_KbArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KbArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 1. Categories that only an article's text names. The key is lower(btrim(...)); a blank text is "General".
            migrationBuilder.Sql("""
                WITH Variants AS (
                    SELECT COALESCE(NULLIF(btrim("Category"), ''), 'General') AS "Name",
                           lower(COALESCE(NULLIF(btrim("Category"), ''), 'General')) AS "Key",
                           count(*) AS "Uses",
                           min("Id") AS "FirstId"
                    FROM "KbArticles"
                    GROUP BY 1, 2),
                Missing AS (
                    SELECT DISTINCT ON (v."Key") v."Name"
                    FROM Variants v
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "KbCategories" c WHERE lower(btrim(c."Name")) = v."Key")
                    ORDER BY v."Key", v."Uses" DESC, v."FirstId")
                INSERT INTO "KbCategories" ("Name", "Slug", "Description", "IsHidden", "ParentCategoryId")
                SELECT "Name", '', '', FALSE, NULL FROM Missing ORDER BY "Name";
                """);

            // 2. Every article joins the lowest-id category its text matches.
            migrationBuilder.Sql("""
                UPDATE "KbArticles" a
                SET "CategoryId" = (
                    SELECT c."Id"
                    FROM "KbCategories" c
                    WHERE lower(btrim(c."Name")) = lower(COALESCE(NULLIF(btrim(a."Category"), ''), 'General'))
                    ORDER BY c."Id"
                    LIMIT 1);
                """);

            // 3. Slugs, one row at a time in id order, each checked against the ones already given. The slug of a text is built
            //    by one function (below) for categories and articles alike: accents are dropped the way the application's
            //    SlugGenerator does (the letters that decompose into a base letter and accents, taken from the Unicode
            //    tables, plus the few that do not: sharp s, ae, oe, o with stroke, d with stroke, l with stroke), the text is cut
            //    to 140 characters and only then checked for being empty or purely numeric. One difference is kept on purpose:
            //    a text with no Latin letters is named by its id here (article-12), while the application names it article and
            //    then article-2, article-3 and so on, because here the id is at hand and makes the name stable.
            migrationBuilder.Sql("""
                CREATE FUNCTION pg_temp."KbSlugBase"(source text, fallback text, rowid integer) RETURNS text AS $fn$
                DECLARE
                    t text;
                BEGIN
                    t := replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(source,
                        'ẞ', 'ss'), 'ß', 'ss'), 'Æ', 'ae'), 'æ', 'ae'), 'Œ', 'oe'), 'œ', 'oe'),
                        'Ø', 'o'), 'ø', 'o'), 'Đ', 'd'), 'đ', 'd'), 'Ł', 'l'), 'ł', 'l');
                    t := lower(translate(t, 'ÀÁÂÃÄÅÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜÝàáâãäåçèéêëìíîïñòóôõöùúûüýÿĀāĂăĄąĆćĈĉĊċČčĎďĒēĔĕĖėĘęĚěĜĝĞğĠġĢģĤĥĨĩĪīĬĭĮįİĴĵĶķĹĺĻļĽľŃńŅņŇňŌōŎŏŐőŔŕŖŗŘřŚśŜŝŞşŠšŢţŤťŨũŪūŬŭŮůŰűŲųŴŵŶŷŸŹźŻżŽžƠơƯưǍǎǏǐǑǒǓǔǕǖǗǘǙǚǛǜǞǟǠǡǦǧǨǩǪǫǬǭǰǴǵǸǹǺǻȀȁȂȃȄȅȆȇȈȉȊȋȌȍȎȏȐȑȒȓȔȕȖȗȘșȚțȞȟȦȧȨȩȪȫȬȭȮȯȰȱȲȳḀḁḂḃḄḅḆḇḈḉḊḋḌḍḎḏḐḑḒḓḔḕḖḗḘḙḚḛḜḝḞḟḠḡḢḣḤḥḦḧḨḩḪḫḬḭḮḯḰḱḲḳḴḵḶḷḸḹḺḻḼḽḾḿṀṁṂṃṄṅṆṇṈṉṊṋṌṍṎṏṐṑṒṓṔṕṖṗṘṙṚṛṜṝṞṟṠṡṢṣṤṥṦṧṨṩṪṫṬṭṮṯṰṱṲṳṴṵṶṷṸṹṺṻṼṽṾṿẀẁẂẃẄẅẆẇẈẉẊẋẌẍẎẏẐẑẒẓẔẕẖẗẘẙẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊịỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹ', 'aaaaaaceeeeiiiinooooouuuuyaaaaaaceeeeiiiinooooouuuuyyaaaaaaccccccccddeeeeeeeeeegggggggghhiiiiiiiiijjkkllllllnnnnnnoooooorrrrrrssssssssttttuuuuuuuuuuuuwwyyyzzzzzzoouuaaiioouuuuuuuuuuaaaaggkkoooojggnnaaaaaaeeeeiiiioooorrrruuuusstthhaaeeooooooooyyaabbbbbbccddddddddddeeeeeeeeeeffgghhhhhhhhhhiiiikkkkkkllllllllmmmmmmnnnnnnnnoooooooopppprrrrrrrrssssssssssttttttttuuuuuuuuuuvvvvwwwwwwwwwwxxxxyyzzzzzzhtwyaaaaaaaaaaaaaaaaaaaaaaaaeeeeeeeeeeeeeeeeiiiioooooooooooooooooooooooouuuuuuuuuuuuuuyyyyyyyy'));
                    t := btrim(regexp_replace(t, '[^a-z0-9]+', '-', 'g'), '-');
                    t := btrim(left(t, 140), '-');
                    IF t = '' THEN
                        t := fallback || '-' || rowid;
                    ELSIF t ~ '^[0-9]+$' THEN
                        t := fallback || '-' || t;
                    END IF;
                    RETURN t;
                END
                $fn$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    r record;
                    t text;
                    candidate text;
                    n integer;
                BEGIN
                    FOR r IN SELECT "Id", "Name" FROM "KbCategories" ORDER BY "Id" LOOP
                        t := pg_temp."KbSlugBase"(r."Name", 'category', r."Id");
                        candidate := t;
                        n := 1;
                        WHILE EXISTS (SELECT 1 FROM "KbCategories" WHERE "Slug" = candidate) LOOP
                            n := n + 1;
                            candidate := t || '-' || n;
                        END LOOP;
                        UPDATE "KbCategories" SET "Slug" = candidate WHERE "Id" = r."Id";
                    END LOOP;

                    FOR r IN SELECT "Id", "Title" FROM "KbArticles" ORDER BY "Id" LOOP
                        t := pg_temp."KbSlugBase"(r."Title", 'article', r."Id");
                        candidate := t;
                        n := 1;
                        WHILE candidate IN ('articles', 'categories')
                              OR EXISTS (SELECT 1 FROM "KbArticles" WHERE "Slug" = candidate) LOOP
                            n := n + 1;
                            candidate := t || '-' || n;
                        END LOOP;
                        UPDATE "KbArticles" SET "Slug" = candidate WHERE "Id" = r."Id";
                    END LOOP;
                END $$;
                """);

            migrationBuilder.Sql("""DROP FUNCTION pg_temp."KbSlugBase"(text, text, integer);""");

            // 4. One translation per article, in the language its own text is written in.
            migrationBuilder.Sql("""
                INSERT INTO "KbArticleTranslations" ("ArticleId", "Locale", "Title", "Content")
                SELECT a."Id",
                       CASE WHEN a."Title" || a."Content" ~ '[Ա-֏]' THEN 'hy'
                            WHEN a."Title" || a."Content" ~ '[Ѐ-ӿ]' THEN 'ru'
                            ELSE 'en' END,
                       a."Title",
                       a."Content"
                FROM "KbArticles" a
                ORDER BY a."Id";
                """);

            // The columns only needed a value to be added; the model has none.
            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "KbCategories",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "KbArticles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "KbArticles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.DropIndex(
                name: "IX_KbArticles_Category",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "KbArticles");

            migrationBuilder.CreateIndex(
                name: "IX_KbCategories_Slug",
                table: "KbCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_KbCategories_SlugNotNumeric",
                table: "KbCategories",
                sql: "\"Slug\" !~ '^[0-9]+$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KbCategories_SlugShape",
                table: "KbCategories",
                sql: "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");

            migrationBuilder.CreateIndex(
                name: "IX_KbArticles_CategoryId",
                table: "KbArticles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KbArticles_Slug",
                table: "KbArticles",
                column: "Slug",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_KbArticles_SlugNotNumeric",
                table: "KbArticles",
                sql: "\"Slug\" !~ '^[0-9]+$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KbArticles_SlugNotReserved",
                table: "KbArticles",
                sql: "\"Slug\" NOT IN ('articles', 'categories')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KbArticles_SlugShape",
                table: "KbArticles",
                sql: "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");

            migrationBuilder.CreateIndex(
                name: "IX_KbArticleTranslations_ArticleId_Locale",
                table: "KbArticleTranslations",
                columns: new[] { "ArticleId", "Locale" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_KbArticles_KbCategories_CategoryId",
                table: "KbArticles",
                column: "CategoryId",
                principalTable: "KbCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "KbArticles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "KbArticles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "KbArticles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            // The text comes back from the en translation, else from the first locale in order; the category comes back as
            // its name. The other languages, the slugs and the original spelling of a merged category are not kept.
            migrationBuilder.Sql("""
                UPDATE "KbArticles" a
                SET "Title" = t."Title",
                    "Content" = t."Content"
                FROM (
                    SELECT DISTINCT ON ("ArticleId") "ArticleId", "Title", "Content"
                    FROM "KbArticleTranslations"
                    ORDER BY "ArticleId", ("Locale" <> 'en'), "Locale") t
                WHERE a."Id" = t."ArticleId";
                """);
            migrationBuilder.Sql("""
                UPDATE "KbArticles" a
                SET "Category" = c."Name"
                FROM "KbCategories" c
                WHERE c."Id" = a."CategoryId";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_KbArticles_KbCategories_CategoryId",
                table: "KbArticles");

            migrationBuilder.DropTable(
                name: "KbArticleTranslations");

            migrationBuilder.DropIndex(
                name: "IX_KbCategories_Slug",
                table: "KbCategories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KbCategories_SlugNotNumeric",
                table: "KbCategories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KbCategories_SlugShape",
                table: "KbCategories");

            migrationBuilder.DropIndex(
                name: "IX_KbArticles_CategoryId",
                table: "KbArticles");

            migrationBuilder.DropIndex(
                name: "IX_KbArticles_Slug",
                table: "KbArticles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KbArticles_SlugNotNumeric",
                table: "KbArticles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KbArticles_SlugNotReserved",
                table: "KbArticles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KbArticles_SlugShape",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "KbCategories");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "KbArticles");

            migrationBuilder.CreateIndex(
                name: "IX_KbArticles_Category",
                table: "KbArticles",
                column: "Category");
        }
    }
}
