using Innovayse.Hostpanel.API.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// The masking of campaign mail tokens in request log lines: the enricher runs in a real Serilog logger, on an event shaped like
/// the one request logging writes.
/// </summary>
public sealed class CampaignTokenPathEnricherTests
{
    /// <summary>Writes a request log line for a path through a logger that carries the enricher.</summary>
    /// <param name="path">The request path.</param>
    /// <returns>The event the sink received.</returns>
    private static LogEvent Write(string path)
    {
        // The filter runs after the enrichers and sees the event the sinks would get; excluding it keeps the test sink-free.
        var received = new List<LogEvent>();
        using var logger = new LoggerConfiguration()
            .Enrich.With<CampaignTokenPathEnricher>()
            .Filter.ByExcluding(logEvent =>
            {
                received.Add(logEvent);
                return true;
            })
            .CreateLogger();
        var template = new MessageTemplateParser().Parse("HTTP {RequestMethod} {RequestPath} responded {StatusCode}");
        var properties = new[]
        {
            new LogEventProperty("RequestMethod", new ScalarValue("GET")),
            new LogEventProperty("RequestPath", new ScalarValue(path)),
            new LogEventProperty("StatusCode", new ScalarValue(200)),
        };
        logger.Write(new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, template, properties));
        return received.Single();
    }

    /// <summary>The token of an open or click address is replaced in the property and in the rendered line.</summary>
    /// <param name="path">The request path.</param>
    /// <param name="expected">The path as logged.</param>
    [Theory]
    [InlineData("/api/public/campaign-mail/open/abc123", "/api/public/campaign-mail/open/***")]
    [InlineData("/api/public/campaign-mail/click/abc123/2", "/api/public/campaign-mail/click/***/2")]
    [InlineData("/API/Public/Campaign-Mail/Open/abc123", "/API/Public/Campaign-Mail/Open/***")]
    public void TrackingPath_HasItsTokenMasked(string path, string expected)
    {
        var logged = Write(path);

        ((ScalarValue)logged.Properties["RequestPath"]).Value.Should().Be(expected);
        logged.RenderMessage().Should().Contain(expected).And.NotContain("abc123");
    }

    /// <summary>Any other path, and a tracking prefix with no token, is logged as it is.</summary>
    /// <param name="path">The request path.</param>
    [Theory]
    [InlineData("/api/public/unsubscribe/one-click")]
    [InlineData("/api/public/campaign-mail/open")]
    [InlineData("/api/public/campaign-mail/other/abc123")]
    [InlineData("/api/admin/email-campaigns/5")]
    public void OtherPath_IsLeftAlone(string path)
    {
        ((ScalarValue)Write(path).Properties["RequestPath"]).Value.Should().Be(path);
    }
}
