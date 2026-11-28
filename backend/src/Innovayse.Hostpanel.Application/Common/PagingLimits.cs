namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// The bounds every list shares: the page-size limits and the longest search text, so the figure the validators
/// enforce and the figure the handlers clamp to cannot drift apart.
/// </summary>
public static class PagingLimits
{
    /// <summary>The smallest page a list may ask for.</summary>
    public const int MinPageSize = 1;

    /// <summary>The largest page an ordinary list may ask for.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The largest page a report export may ask for.</summary>
    public const int MaxReportPageSize = 200;

    /// <summary>The longest search or text-filter value a list accepts; far above any real name, address or number.</summary>
    public const int MaxSearchLength = 200;

    /// <summary>
    /// How many ids one id search (the clients, the services) answers with. A list turns the answer into an <c>IN</c>
    /// clause, so a search that matches more rows than this would build a query the database has to be talked into
    /// accepting; the answer is then the lowest ids and the person narrows the text.
    /// </summary>
    public const int MaxSearchMatches = 500;
}
