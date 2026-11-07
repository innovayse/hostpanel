namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// WHM API 1 reply bodies in the shapes the cPanel API reference and its mock server give for each
/// function. None of them comes from a live WHM server.
/// </summary>
internal static class WhmReplies
{
    /// <summary>A call that succeeded with no data.</summary>
    public const string Ok = """{"metadata":{"command":"suspendacct","reason":"OK","result":1,"version":1}}""";

    /// <summary>A refusal whose reason names the account and must never be repeated.</summary>
    public const string Refused = """{"metadata":{"command":"createacct","reason":"Sorry, testuser is taken.","result":0,"version":1}}""";

    /// <summary>A <c>createacct</c> that succeeded.</summary>
    public const string Created = """{"metadata":{"command":"createacct","reason":"Account Creation Ok","result":1,"version":1},"data":{"ip":"192.0.2.10","package":"Basic"}}""";

    /// <summary>A <c>listaccts</c> answer holding the active account.</summary>
    public const string ActiveAccount = """{"metadata":{"command":"listaccts","reason":"OK","result":1,"version":1},"data":{"acct":[{"user":"testuser","domain":"example.com","suspended":0,"plan":"Basic","ip":"192.0.2.10"}]}}""";

    /// <summary>A <c>listaccts</c> answer holding the suspended account.</summary>
    public const string SuspendedAccount = """{"metadata":{"command":"listaccts","reason":"OK","result":1,"version":1},"data":{"acct":[{"user":"testuser","domain":"example.com","suspended":1,"suspendreason":"Overdue","plan":"Basic"}]}}""";

    /// <summary>A <c>listaccts</c> answer holding the account on the Pro package.</summary>
    public const string ProAccount = """{"metadata":{"command":"listaccts","reason":"OK","result":1,"version":1},"data":{"acct":[{"user":"testuser","domain":"example.com","suspended":0,"plan":"Pro"}]}}""";

    /// <summary>A <c>listaccts</c> answer that matched nothing.</summary>
    public const string NoAccount = """{"metadata":{"command":"listaccts","reason":"OK","result":1,"version":1},"data":{"acct":[]}}""";

    /// <summary>A <c>create_user_session</c> that succeeded.</summary>
    public const string Session = """{"metadata":{"command":"create_user_session","reason":"OK","result":1,"version":1},"data":{"url":"https://whm.example.test:2083/cpsess123/login/?session=testuser:abc","expires":1700000000}}""";

    /// <summary>A <c>version</c> answer.</summary>
    public const string Version = """{"metadata":{"command":"version","reason":"OK","result":1,"version":1},"data":{"version":"11.126.0.5"}}""";

    /// <summary>A body that is not a WHM reply, e.g. a proxy error page.</summary>
    public const string ErrorPage = "<html>Bad Gateway</html>";
}
