namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>One request the fake plugin registry received, as it arrived: the method, the target on the wire and the headers.</summary>
/// <param name="Method">The HTTP method, upper case.</param>
/// <param name="Target">The path and query exactly as sent.</param>
/// <param name="Headers">Every request header, by name, compared without case; values joined with a comma.</param>
/// <param name="Body">The request body text; empty for a request without one.</param>
public sealed record RecordedRegistryCall(string Method, string Target, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>Gets whether the request carried the signature headers of an instance.</summary>
    public bool IsSigned => Headers.ContainsKey("X-Hostpanel-Signature");

    /// <summary>Gets whether the request carried a licence key.</summary>
    public bool HasLicenceKey => Headers.ContainsKey("X-Licence-Key");
}
