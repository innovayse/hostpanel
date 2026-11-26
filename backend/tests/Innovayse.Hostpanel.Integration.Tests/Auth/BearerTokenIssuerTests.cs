using System.Buffers.Text;
using System.Text;
using Innovayse.Hostpanel.API.Auth;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// The reader the scheme selector asks which issuer a token names answers for every header a caller can send,
/// and never throws: an unreadable token is just a token with no issuer, refused later like any invalid one.
/// </summary>
public sealed class BearerTokenIssuerTests
{
    /// <summary>The header part of a compact token; its content is never read.</summary>
    private const string Header = "eyJhbGciOiJIUzI1NiJ9";

    /// <summary>The signature part of a compact token; its content is never read.</summary>
    private const string Signature = "signature";

    /// <summary>A compact token whose payload names an issuer yields that issuer.</summary>
    [Fact]
    public void ATokenWithAnIssuerYieldsIt()
    {
        var token = $"{Header}.{Encode("""{"iss":"https://issuer.example","sub":"1"}""")}.{Signature}";

        var read = BearerTokenIssuer.TryRead(token, out var issuer);

        read.Should().BeTrue();
        issuer.Should().Be("https://issuer.example");
    }

    /// <summary>Anything that is not a compact token with a string issuer is answered false, with no exception.</summary>
    /// <param name="token">The text a caller put after <c>Bearer </c>.</param>
    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("..")]
    [InlineData("a..c")]
    [InlineData("a.!!!.c")]
    [InlineData("e30.%%%.signature")]
    [InlineData("a.b.c")]
    [InlineData("a.bm90IGpzb24.c")]
    public void AnUnreadableTokenYieldsNoIssuer(string token)
    {
        var read = BearerTokenIssuer.TryRead(token, out var issuer);

        read.Should().BeFalse();
        issuer.Should().BeNull();
    }

    /// <summary>A payload that is JSON but names no usable issuer yields none.</summary>
    /// <param name="payload">The payload text, before encoding.</param>
    [Theory]
    [InlineData("[]")]
    [InlineData("\"iss\"")]
    [InlineData("{}")]
    [InlineData("""{"iss":7}""")]
    [InlineData("""{"iss":null}""")]
    public void APayloadWithoutAStringIssuerYieldsNoIssuer(string payload)
    {
        var read = BearerTokenIssuer.TryRead($"{Header}.{Encode(payload)}.{Signature}", out var issuer);

        read.Should().BeFalse();
        issuer.Should().BeNull();
    }

    /// <summary>Encodes text the way a token's payload segment is encoded.</summary>
    /// <param name="text">The payload text.</param>
    /// <returns>The base64url form without padding.</returns>
    private static string Encode(string text) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text));
}
