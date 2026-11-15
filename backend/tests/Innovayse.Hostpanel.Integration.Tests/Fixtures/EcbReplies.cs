using System.Globalization;
using System.Text;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Bodies the European Central Bank's daily reference rates feed answers with, in the shape the bank
/// publishes (the <c>eurofxref</c> namespace, one dated <c>Cube</c> holding a <c>Cube</c> per currency),
/// for a <see cref="StubHttpServer"/> to replay.
/// </summary>
public static class EcbReplies
{
    /// <summary>The media type the bank serves the document with.</summary>
    public const string ContentType = "text/xml";

    /// <summary>A real-shaped set for 2026-11-13: how many units of each currency one euro buys.</summary>
    public static readonly string Daily = Document("2026-11-13", ("USD", "1.2500"), ("GBP", "0.8500"), ("JPY", "160.00"), ("RUB", "100.00"));

    /// <summary>Builds the document the bank publishes for one working day.</summary>
    /// <param name="date">The day the set is published for, as <c>yyyy-MM-dd</c>.</param>
    /// <param name="rates">The currencies with the units of each that one euro buys, as the bank writes them.</param>
    /// <returns>The XML text.</returns>
    public static string Document(string date, params (string Code, string Rate)[] rates)
    {
        var cubes = new StringBuilder();
        foreach (var (code, rate) in rates)
        {
            cubes.Append(CultureInfo.InvariantCulture, $"<Cube currency='{code}' rate='{rate}'/>");
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
              <gesmes:subject>Reference rates</gesmes:subject>
              <gesmes:Sender><gesmes:name>European Central Bank</gesmes:name></gesmes:Sender>
              <Cube><Cube time='{date}'>{cubes}</Cube></Cube>
            </gesmes:Envelope>
            """;
    }
}
