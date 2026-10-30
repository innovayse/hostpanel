namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The one place that says which namespaces the core must never name. A deployment-specific plugin lives
/// under <c>Innovayse.&lt;Segment&gt;</c> (the second segment of its namespace), and the core assemblies
/// (Domain, Application, Infrastructure, API) stay neutral: they reach such code only through an SDK
/// contract loaded at run time. To reserve another namespace add its segment to <see cref="Segments"/>;
/// to let the core use one existing, optional package add its namespace prefix to <see cref="Allowed"/>.
/// </summary>
internal static class ReservedPluginNamespaces
{
    /// <summary>The first segment of every namespace this rule looks at; deployment plugins share the product root.</summary>
    public const string Root = "Innovayse";

    /// <summary>
    /// The reserved second segments. <c>Plugins</c>, <c>Deployment</c> and <c>Custom</c> are where a deployment
    /// puts its own code; <c>Auth</c> is the external identity provider's client package, which the core may
    /// use only where <see cref="Allowed"/> says so. The in-tree <c>Hostpanel</c>, <c>SDK</c> and <c>Providers</c>
    /// roots are not reserved: the SDK is the contract and the providers have their own rules.
    /// </summary>
    public static readonly IReadOnlyList<string> Segments = ["Plugins", "Deployment", "Custom", "Auth"];

    /// <summary>
    /// Namespace prefixes exempt from the reservation: the optional identity provider package, referenced only
    /// by an <c>InnovayseSso=true</c> build (see <c>backend/Directory.Build.props</c>), behind <c>#if INNOVAYSE_SSO</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Allowed = ["Innovayse.Auth"];
}
