using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="PortalSettingKeys"/>: the invariants that keep the seed list, the
/// public allow-list and the value vocabularies from drifting apart. Nothing else would notice:
/// a key seeded but not public just renders the build-time default, silently.
/// </summary>
/// <remarks>The type under test is a Domain policy class with no Application dependency.</remarks>
public class PortalSettingKeysTests
{
    /// <summary>Every seeded key except the operator legal texts is public and every public key is seeded.</summary>
    [Fact]
    public void Public_MatchesDefaults_Exactly()
    {
        var seeded = PortalSettingKeys.Defaults.Select(d => d.Key).Where(key => !PortalSettingKeys.IsLegalText(key)).ToHashSet();

        Assert.Equal(seeded, PortalSettingKeys.Public);
    }

    /// <summary>A seeded key appears once; a duplicate would make the seeder insert it twice.</summary>
    [Fact]
    public void Defaults_HaveNoDuplicateKeys()
    {
        var keys = PortalSettingKeys.Defaults.Select(d => d.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    /// <summary>Every key with a vocabulary is a seeded, public key.</summary>
    [Fact]
    public void AllowedValues_OnlyNameSeededKeys()
    {
        foreach (var key in PortalSettingKeys.AllowedValues.Keys)
        {
            Assert.Contains(key, PortalSettingKeys.Public);
        }
    }

    /// <summary>
    /// The value a fresh install starts from must itself be writable: in the vocabulary, or
    /// empty for a key that allows empty.
    /// </summary>
    [Fact]
    public void Defaults_ForVocabularyKeys_AreAllowed()
    {
        foreach (var (key, value, _) in PortalSettingKeys.Defaults)
        {
            if (PortalSettingKeys.AllowedValues.TryGetValue(key, out var allowed))
            {
                Assert.True(
                    allowed.Contains(value) || (value.Length == 0 && PortalSettingKeys.AllowsEmpty(key)),
                    $"{key} seeds '{value}', which it would refuse");
            }
        }
    }

    /// <summary>Every shaped key is seeded, public, and seeds empty; the shape only applies to a set value.</summary>
    [Fact]
    public void Patterns_OnlyNameSeededKeysThatAllowEmpty()
    {
        foreach (var key in PortalSettingKeys.Patterns.Keys)
        {
            Assert.Contains(key, PortalSettingKeys.Public);
            Assert.True(PortalSettingKeys.AllowsEmpty(key), $"{key} must seed empty");
            Assert.False(
                PortalSettingKeys.AllowedValues.ContainsKey(key), $"{key} cannot have both a vocabulary and a shape");
        }
    }

    /// <summary>A key that seeds with a value never accepts an empty one.</summary>
    [Fact]
    public void AllowsEmpty_IsFalseForKeysThatSeedWithAValue()
    {
        Assert.False(PortalSettingKeys.AllowsEmpty(PortalSettingKeys.Template));
        Assert.False(PortalSettingKeys.AllowsEmpty(PortalSettingKeys.ThemeDefault));
        Assert.True(PortalSettingKeys.AllowsEmpty(PortalSettingKeys.BrandPrimary));
        Assert.True(PortalSettingKeys.AllowsEmpty(PortalSettingKeys.BrandFontBody));
        Assert.False(PortalSettingKeys.AllowsEmpty("integration:cwp7:hostname"));
    }

    /// <summary>A fresh install seeds the classic template, spelled as the storefront matches it.</summary>
    [Fact]
    public void Defaults_SeedTheClassicTemplate()
    {
        Assert.Equal(StorefrontTemplate.Classic, PortalSettingKeys.DefaultTemplate);
        Assert.Equal("classic", PortalSettingKeys.Defaults.Single(d => d.Key == PortalSettingKeys.Template).Value);
    }

    /// <summary>The template vocabulary matches what the storefront's registry can render.</summary>
    [Fact]
    public void TemplateNames_MatchTheClientRegistry()
    {
        Assert.Equal(
            new[] { "aurora", "classic", "nova" },
            PortalSettingKeys.TemplateNames.OrderBy(n => n).ToArray());
    }

    /// <summary>The hero figures and the office address and map are seeded empty and public.</summary>
    [Fact]
    public void HeroAndOfficeKeys_AreSeededEmptyAndPublic()
    {
        string[] keys =
        [
            PortalSettingKeys.StatsClients,
            PortalSettingKeys.StatsUptime,
            PortalSettingKeys.StatsRegions,
            PortalSettingKeys.HeroBadge,
            PortalSettingKeys.ContactAddress,
            PortalSettingKeys.ContactMapUrl,
        ];

        Assert.Equal(
            ["portal.stats.clients", "portal.stats.uptime", "portal.stats.regions", "portal.hero.badge", "portal.contact.address", "portal.contact.map_url"],
            keys);
        foreach (var key in keys)
        {
            Assert.Contains(key, PortalSettingKeys.Public);
            Assert.True(PortalSettingKeys.AllowsEmpty(key), $"{key} must seed empty so no figure is invented");
        }
    }

    /// <summary>The social, hours, About and nova switch keys are seeded empty and public; the switches accept only booleans.</summary>
    [Fact]
    public void StorefrontExtras_AreSeededEmptyAndPublic()
    {
        string[] switches =
        [
            PortalSettingKeys.FeaturesBackups,
            PortalSettingKeys.FeaturesStorage,
            PortalSettingKeys.FeaturesCaching,
            PortalSettingKeys.FeaturesCdn,
            PortalSettingKeys.FeaturesFirewall,
            PortalSettingKeys.FeaturesMalware,
            PortalSettingKeys.MigrationFree,
        ];
        string[] keys =
        [
            PortalSettingKeys.SocialTwitter,
            PortalSettingKeys.ContactHours,
            PortalSettingKeys.ContactTimezone,
            PortalSettingKeys.AboutText,
            PortalSettingKeys.AboutTextRu,
            PortalSettingKeys.AboutTextHy,
            .. switches,
        ];

        foreach (var key in keys)
        {
            Assert.Contains(key, PortalSettingKeys.Public);
            Assert.True(PortalSettingKeys.AllowsEmpty(key), $"{key} must seed empty so no claim is invented");
        }

        foreach (var key in switches)
        {
            Assert.Same(PortalSettingKeys.Booleans, PortalSettingKeys.AllowedValues[key]);
        }
    }

    /// <summary>Every operator legal text (five documents, three languages) is seeded empty, editable, free text, under the legal prefix, and kept out of the public set every page carries.</summary>
    [Fact]
    public void LegalTexts_AreSeededEmptyEditableFreeTextAndOutOfThePublicSet()
    {
        string[] keys =
        [
            PortalSettingKeys.LegalTerms, PortalSettingKeys.LegalTermsRu, PortalSettingKeys.LegalTermsHy,
            PortalSettingKeys.LegalPrivacy, PortalSettingKeys.LegalPrivacyRu, PortalSettingKeys.LegalPrivacyHy,
            PortalSettingKeys.LegalRefund, PortalSettingKeys.LegalRefundRu, PortalSettingKeys.LegalRefundHy,
            PortalSettingKeys.LegalAcceptableUse, PortalSettingKeys.LegalAcceptableUseRu, PortalSettingKeys.LegalAcceptableUseHy,
            PortalSettingKeys.LegalCookies, PortalSettingKeys.LegalCookiesRu, PortalSettingKeys.LegalCookiesHy,
        ];

        Assert.Equal(keys.Length, keys.Distinct().Count());
        foreach (var key in keys)
        {
            Assert.StartsWith("portal.legal.", key);
            Assert.DoesNotContain(key, PortalSettingKeys.Public);
            Assert.Contains(key, GeneralSettingKeys.All);
            Assert.True(PortalSettingKeys.IsLegalText(key), $"{key} is a legal text");
            Assert.True(PortalSettingKeys.AllowsEmpty(key), $"{key} must seed empty so the built-in text stays");
            Assert.False(PortalSettingKeys.Patterns.ContainsKey(key), $"{key} is free text with no shape");
            Assert.False(PortalSettingKeys.AllowedValues.ContainsKey(key), $"{key} is free text with no vocabulary");
        }
    }

    /// <summary>The page slugs list the same fifteen keys, three per page, default language first, and nothing else is a legal text.</summary>
    [Fact]
    public void LegalTexts_ListThreeRowsPerPage_AndNothingElseIsALegalText()
    {
        Assert.Equal(new[] { "terms", "privacy", "refund", "acceptable_use", "cookies" }, PortalSettingKeys.LegalTexts.Keys.ToArray());
        foreach (var (page, keys) in PortalSettingKeys.LegalTexts)
        {
            Assert.Equal(3, keys.Count);
            Assert.Equal($"portal.legal.{page}", keys[0]);
            Assert.Equal($"portal.legal.{page}.ru", keys[1]);
            Assert.Equal($"portal.legal.{page}.hy", keys[2]);
        }

        Assert.False(PortalSettingKeys.IsLegalText("portal.legal.tax_id"), "the tax id is a short ordinary setting");
        Assert.False(PortalSettingKeys.IsLegalText(PortalSettingKeys.AboutText));
    }

    /// <summary>The home-page section toggles seed "true" (projects "false"), are public, and accept only booleans, never empty.</summary>
    [Fact]
    public void SectionToggles_SeedVisible_ExceptProjects_AndAcceptOnlyBooleans()
    {
        string[] sections =
        [
            PortalSettingKeys.SectionStats,
            PortalSettingKeys.SectionPartners,
            PortalSettingKeys.SectionServices,
            PortalSettingKeys.SectionProcess,
            PortalSettingKeys.SectionPlans,
            PortalSettingKeys.SectionComparison,
            PortalSettingKeys.SectionDashboard,
            PortalSettingKeys.SectionUseCases,
            PortalSettingKeys.SectionMigration,
            PortalSettingKeys.SectionProjects,
            PortalSettingKeys.SectionTestimonials,
            PortalSettingKeys.SectionFaq,
            PortalSettingKeys.SectionCta,
            PortalSettingKeys.SectionLocations,
            PortalSettingKeys.SectionPerformance,
            PortalSettingKeys.SectionSecurity,
            PortalSettingKeys.SectionApps,
            PortalSettingKeys.SectionGuarantees,
            PortalSettingKeys.SectionSupport,
            PortalSettingKeys.SectionTrusted,
        ];

        Assert.Equal(20, sections.Length);
        Assert.All(sections, key => Assert.StartsWith("portal.sections.", key));
        foreach (var key in sections)
        {
            var seeded = PortalSettingKeys.Defaults.Single(d => d.Key == key);
            Assert.Equal(key == PortalSettingKeys.SectionProjects ? "false" : "true", seeded.Value);
            Assert.Contains(key, PortalSettingKeys.Public);
            Assert.False(PortalSettingKeys.AllowsEmpty(key), $"{key} always has a value");
            Assert.Same(PortalSettingKeys.Booleans, PortalSettingKeys.AllowedValues[key]);
        }
    }

    /// <summary>The performance, security, installer, guarantee and support-response keys seed empty and public so nothing is invented.</summary>
    [Fact]
    public void HostingFactKeys_AreSeededEmptyAndPublic()
    {
        string[] keys =
        [
            PortalSettingKeys.StatsResponseTime,
            PortalSettingKeys.StatsSitesHosted,
            PortalSettingKeys.StatsSupportResponse,
            PortalSettingKeys.FeaturesSsl,
            PortalSettingKeys.FeaturesDdos,
            PortalSettingKeys.InstallerAppCount,
            PortalSettingKeys.GuaranteeMoneyBackDays,
            PortalSettingKeys.GuaranteeNoHiddenFees,
            PortalSettingKeys.SupportResponseChat,
            PortalSettingKeys.SupportResponseTicket,
            PortalSettingKeys.SupportResponseEmail,
            PortalSettingKeys.SupportResponsePhone,
        ];

        foreach (var key in keys)
        {
            Assert.Contains(key, PortalSettingKeys.Public);
            Assert.True(PortalSettingKeys.AllowsEmpty(key), $"{key} must seed empty so no claim is invented");
        }

        Assert.Same(PortalSettingKeys.Booleans, PortalSettingKeys.AllowedValues[PortalSettingKeys.FeaturesSsl]);
        Assert.Same(PortalSettingKeys.Booleans, PortalSettingKeys.AllowedValues[PortalSettingKeys.FeaturesDdos]);
        Assert.Same(PortalSettingKeys.Booleans, PortalSettingKeys.AllowedValues[PortalSettingKeys.GuaranteeNoHiddenFees]);
    }

    /// <summary>The money-back days accept one to three digits and nothing else.</summary>
    /// <param name="value">The value to test.</param>
    /// <param name="valid">Whether the shape should accept it.</param>
    [Theory]
    [InlineData("30", true)]
    [InlineData("7", true)]
    [InlineData("365", true)]
    [InlineData("1000", false)]
    [InlineData("30 days", false)]
    [InlineData("-5", false)]
    public void MoneyBackDays_PatternAcceptsOnlyShortDigitStrings(string value, bool valid)
    {
        var pattern = PortalSettingKeys.Patterns[PortalSettingKeys.GuaranteeMoneyBackDays];

        Assert.Equal(valid, pattern.Regex.IsMatch(value));
    }

    /// <summary>The per-language address and hours are seeded empty and public, beside the default ones.</summary>
    [Fact]
    public void ContactLocaleKeys_AreSeededEmptyAndPublic()
    {
        string[] keys =
        [
            PortalSettingKeys.ContactAddressRu,
            PortalSettingKeys.ContactAddressHy,
            PortalSettingKeys.ContactHoursRu,
            PortalSettingKeys.ContactHoursHy,
        ];

        Assert.Equal(
            ["portal.contact.address.ru", "portal.contact.address.hy", "portal.contact.hours.ru", "portal.contact.hours.hy"],
            keys);
        foreach (var key in keys)
        {
            Assert.Contains(key, PortalSettingKeys.Public);
            Assert.True(PortalSettingKeys.AllowsEmpty(key), $"{key} must seed empty so the default text is used");
        }
    }

    /// <summary>The contact email accepts one address with a dotted domain and nothing else.</summary>
    /// <param name="value">The value to test.</param>
    /// <param name="valid">Whether the shape should accept it.</param>
    [Theory]
    [InlineData("support@example.com", true)]
    [InlineData("a.b+c@mail.example.org", true)]
    [InlineData("support", false)]
    [InlineData("support@example", false)]
    [InlineData("two words@example.com", false)]
    [InlineData("a@b@example.com", false)]
    public void ContactEmail_PatternAcceptsOnlyAnAddress(string value, bool valid)
    {
        var pattern = PortalSettingKeys.Patterns[PortalSettingKeys.ContactEmail];

        Assert.Equal(valid, pattern.Regex.IsMatch(value));
    }

    /// <summary>The map address accepts an https address and refuses any other scheme.</summary>
    /// <param name="value">The value to test.</param>
    /// <param name="valid">Whether the shape should accept it.</param>
    [Theory]
    [InlineData("https://maps.example.com/embed?x=1", true)]
    [InlineData("https://maps.example.com", true)]
    [InlineData("http://maps.example.com/embed", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("maps.example.com", false)]
    public void ContactMapUrl_PatternAcceptsOnlyHttps(string value, bool valid)
    {
        var pattern = PortalSettingKeys.Patterns[PortalSettingKeys.ContactMapUrl];

        Assert.Equal(valid, pattern.Regex.IsMatch(value));
    }

    /// <summary>The time zone accepts an IANA id (or UTC) and refuses the old free-text offset and anything else.</summary>
    /// <param name="value">The value to test.</param>
    /// <param name="valid">Whether the shape should accept it.</param>
    [Theory]
    [InlineData("Asia/Yerevan", true)]
    [InlineData("America/Argentina/Buenos_Aires", true)]
    [InlineData("Etc/GMT-4", true)]
    [InlineData("UTC", true)]
    [InlineData("UTC+4", false)]
    [InlineData("GMT+4", false)]
    [InlineData("Yerevan", false)]
    [InlineData("Asia/Yerevan/../x/y", false)]
    [InlineData("Asia/Yer evan", false)]
    public void ContactTimezone_PatternAcceptsOnlyAnIanaId(string value, bool valid)
    {
        var pattern = PortalSettingKeys.Patterns[PortalSettingKeys.ContactTimezone];

        Assert.Equal(valid, pattern.Regex.IsMatch(value));
    }

    /// <summary>Every social and app launcher link has the https pattern, and it refuses anything else.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="valid">Whether every link key must accept it.</param>
    [Theory]
    [InlineData("https://www.example.com/page", true)]
    [InlineData("http://www.example.com/page", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/uploads/branding/logo.png", false)]
    public void LinkKeys_PatternAcceptsOnlyHttps(string value, bool valid)
    {
        string[] keys =
        [
            PortalSettingKeys.SocialFacebook, PortalSettingKeys.SocialInstagram, PortalSettingKeys.SocialLinkedin,
            PortalSettingKeys.SocialYoutube, PortalSettingKeys.SocialTwitter, PortalSettingKeys.AppsAccount,
            PortalSettingKeys.AppsTasks, PortalSettingKeys.AppsErp, PortalSettingKeys.AppsHostpanel,
            PortalSettingKeys.AppsSheets, PortalSettingKeys.AppsMail, PortalSettingKeys.AppsDocs,
            PortalSettingKeys.AppsCalendar,
        ];

        foreach (var key in keys)
        {
            Assert.Equal(valid, PortalSettingKeys.Patterns[key].Regex.IsMatch(value));
        }
    }

    /// <summary>Every logo key accepts an https address or an uploaded path, and nothing else.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="valid">Whether every logo key must accept it.</param>
    [Theory]
    [InlineData("https://cdn.example.com/logo.svg", true)]
    [InlineData("/uploads/branding/abc/logo.png", true)]
    [InlineData("/etc/passwd", false)]
    [InlineData("http://cdn.example.com/logo.svg", false)]
    [InlineData("javascript:alert(1)", false)]
    public void LogoKeys_PatternAcceptsHttpsOrUploads(string value, bool valid)
    {
        foreach (var key in new[] { PortalSettingKeys.Logo, PortalSettingKeys.LogoDark, PortalSettingKeys.LogoMark })
        {
            Assert.Equal(valid, PortalSettingKeys.Patterns[key].Regex.IsMatch(value));
        }
    }

    /// <summary>Every template is stored as its lower-case name.</summary>
    /// <param name="template">The template.</param>
    /// <param name="expected">The stored value.</param>
    [Theory]
    [InlineData(StorefrontTemplate.Classic, "classic")]
    [InlineData(StorefrontTemplate.Aurora, "aurora")]
    [InlineData(StorefrontTemplate.Nova, "nova")]
    public void TemplateValue_IsTheLowerCaseName(StorefrontTemplate template, string expected)
    {
        Assert.Equal(expected, PortalSettingKeys.TemplateValue(template));
    }
}
