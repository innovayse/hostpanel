using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;

/// <summary>Builds Plesk XML API request packets and reads their replies.</summary>
/// <remarks>
/// Every request carries one operation, so every reply carries one <c>result</c>. The status is
/// read from that element's own <c>status</c> child only: a customer's or a subscription's
/// <c>gen_info</c> holds a numeric <c>status</c> of its own, and reading the first one found in
/// the document would mistake "0" for the call's outcome. A refusal the API raises before it reads
/// the operation (a wrong key, HTTP instead of HTTPS) comes back under <c>system</c> instead.
/// </remarks>
public static class PleskPacket
{
    /// <summary>The status text Plesk answers a successful operation with.</summary>
    private const string OkStatus = "ok";

    /// <summary>Wraps one operation in a request packet.</summary>
    /// <param name="operation">The operation, e.g. <c>&lt;customer&gt;&lt;get&gt;…&lt;/get&gt;&lt;/customer&gt;</c>.</param>
    /// <returns>The request body.</returns>
    public static string Build(XElement operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new XElement("packet", operation).ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>Reads the reply to a one-operation packet.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>
    /// Success with the <c>result</c> element; a refusal with Plesk's error code; or
    /// <see cref="PleskReply.Unreadable"/> when the body is not a reply Plesk would send.
    /// </returns>
    public static PleskReply Parse(string body)
    {
        XElement root;
        try
        {
            root = XElement.Parse(body);
        }
        catch (XmlException)
        {
            return PleskReply.Unreadable;
        }

        if (root.Name != "packet")
        {
            return PleskReply.Unreadable;
        }

        var outcome = root.Element("system") ?? root.Descendants("result").FirstOrDefault();
        if (outcome?.Element("status")?.Value is not { } status)
        {
            return PleskReply.Unreadable;
        }

        if (string.Equals(status, OkStatus, StringComparison.OrdinalIgnoreCase))
        {
            return new PleskReply(true, null, outcome);
        }

        return int.TryParse(outcome.Element("errcode")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? new PleskReply(false, code, null)
            : PleskReply.Unreadable;
    }
}
