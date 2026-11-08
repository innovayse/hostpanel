using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins.Registry;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// The text a restricted plugin's calls are signed over, and the signature itself, against a fixed vector. The registry builds the
/// same bytes from the request it receives, so one differing character is a rejected call. The vector was made outside this code
/// base: the signed text is the registry's own definition (the version tag, the method, the request target as sent, the SHA-256
/// of the empty body, the timestamp and the nonce, joined with line feeds and no trailing one), signed with an independent
/// Ed25519 implementation over a fixed private seed (the bytes 0 to 31).
/// </summary>
public sealed class RegistryCallSigningTests
{
    /// <summary>The private seed of the vector, the bytes 0 to 31, base64.</summary>
    private const string Seed = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    /// <summary>The public key of the vector, base64.</summary>
    private const string PublicKey = "A6EHv/POEL4dcN0Y50vAmWfk1jCbpQ1fHdyGZBJVMbg=";

    /// <summary>The SHA-256 of the empty body, lower-case hex.</summary>
    private const string EmptyBody = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>Each case of the vector: method, target, timestamp, nonce and the signature over the text.</summary>
    public static TheoryData<string, string, string, string, string> Vector => new()
    {
        { "GET", "/v1/plugins/acme-pay/versions/1.2.0", "1760000000", "AAAAAAAAAAAAAAAAAAAAAAAA", "aKC7prysbawfnvGku5z1Tiwg7VeFSisPhSfBVOJVToH42dvtKHMsPCb5wHsiUGj6grWpYP9gZVrfsjmkvSb1BA==" },
        { "POST", "/v1/plugins/acme-pay/versions/latest/download-tickets", "1760000060", "bm9uY2Utb25lLXR3by10aHJlZQ==", "yd4EwUJaOgPIa4rvGxLheHRODxXt5cneGPvejwxpHePETHQgncGgegIIe7ItGR9IK6gXwutkjkwKgRO/9u/8Bg==" },
        { "GET", "/v1/plugins/acme-pay/versions/1.2.0/download?ticket=0123abcd", "1760000120", "k3J9_x-Q8vLw2mZpT4nA", "rfxnnVaZbr25R/QsQaT8xaHdmiE60Fr+7hXkyZFdsQpFPcKlu4H3f20lU5sHQA97E81xiIV71kCCPfpzJQl7BA==" },
    };

    /// <summary>The text is the tag and five fields on six lines, joined with line feeds, with no trailing one.</summary>
    [Fact]
    public void Payload_IsSixLinesWithNoTrailingLineFeed()
    {
        var text = Encoding.UTF8.GetString(
            RegistryCallSigning.Payload("GET", "/v1/plugins/acme-pay/versions/1.2.0", EmptyBody, "1760000000", "AAAAAAAAAAAAAAAAAAAAAAAA"));

        text.Should().Be(
            "HOSTPANEL-REGISTRY-V1\nGET\n/v1/plugins/acme-pay/versions/1.2.0\n"
            + "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\n1760000000\nAAAAAAAAAAAAAAAAAAAAAAAA");
        text.Should().NotEndWith("\n");
        text.Split('\n').Should().HaveCount(6);
    }

    /// <summary>The body hash of a call with no body is the SHA-256 of the empty string in lower-case hex.</summary>
    [Fact]
    public void EmptyBodySha256_IsTheHashOfTheEmptyString()
    {
        RegistryCallSigning.EmptyBodySha256.Should().Be(EmptyBody);
    }

    /// <summary>The query with the ticket is part of the signed target, as the request line carries it.</summary>
    [Fact]
    public void Payload_KeepsTheQueryOfThePackageFetch()
    {
        var text = Encoding.UTF8.GetString(
            RegistryCallSigning.Payload("GET", "/v1/plugins/acme-pay/versions/1.2.0/download?ticket=0123abcd", EmptyBody, "1760000120", "n"));

        text.Split('\n')[2].Should().Be("/v1/plugins/acme-pay/versions/1.2.0/download?ticket=0123abcd");
    }

    /// <summary>The signature of each case of the vector is the one the independent implementation made.</summary>
    /// <param name="method">The method.</param>
    /// <param name="target">The request target.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="signature">The expected signature, base64.</param>
    [Theory]
    [MemberData(nameof(Vector))]
    public void Sign_MatchesTheFixedVector(string method, string target, string timestamp, string nonce, string signature)
    {
        var payload = RegistryCallSigning.Payload(method, target, EmptyBody, timestamp, nonce);

        new Ed25519RegistryInstanceKeys().Sign(Seed, payload).Should().Be(signature);
    }

    /// <summary>The vector's signatures verify under the vector's public key, and a changed target does not.</summary>
    /// <param name="method">The method.</param>
    /// <param name="target">The request target.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="signature">The expected signature, base64.</param>
    [Theory]
    [MemberData(nameof(Vector))]
    public void TheVectorSignature_VerifiesUnderThePublicKeyOnlyForItsOwnText(string method, string target, string timestamp, string nonce, string signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(Convert.FromBase64String(PublicKey), 0));
        var own = RegistryCallSigning.Payload(method, target, EmptyBody, timestamp, nonce);
        verifier.BlockUpdate(own, 0, own.Length);
        verifier.VerifySignature(Convert.FromBase64String(signature)).Should().BeTrue();

        var other = RegistryCallSigning.Payload(method, target + "/", EmptyBody, timestamp, nonce);
        verifier.Reset();
        verifier.BlockUpdate(other, 0, other.Length);
        verifier.VerifySignature(Convert.FromBase64String(signature)).Should().BeFalse();
    }

    /// <summary>A generated pair signs for its own public key, and the private half is not printed.</summary>
    [Fact]
    public void AGeneratedPair_SignsForItsOwnPublicKey_AndPrintsWithoutThePrivateHalf()
    {
        var keys = new Ed25519RegistryInstanceKeys();
        var pair = keys.Generate();
        var payload = RegistryCallSigning.Payload("GET", "/v1/plugins/x/versions", EmptyBody, "1", "n");

        var signature = Convert.FromBase64String(keys.Sign(pair.PrivateKey, payload));

        Convert.FromBase64String(pair.PublicKey).Should().HaveCount(32);
        Convert.FromBase64String(pair.PrivateKey).Should().HaveCount(32);
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(Convert.FromBase64String(pair.PublicKey), 0));
        verifier.BlockUpdate(payload, 0, payload.Length);
        verifier.VerifySignature(signature).Should().BeTrue();
        pair.ToString().Should().NotContain(pair.PrivateKey).And.Contain(pair.PublicKey);
    }

    /// <summary>Two pairs differ, so no installation shares an identity with another.</summary>
    [Fact]
    public void TwoGeneratedPairs_Differ()
    {
        var keys = new Ed25519RegistryInstanceKeys();

        keys.Generate().PublicKey.Should().NotBe(keys.Generate().PublicKey);
    }

    /// <summary>A nonce is 16 to 64 characters of the allowed set, and differs on every call.</summary>
    [Fact]
    public void ANonce_IsInTheAcceptedShape_AndNeverRepeats()
    {
        var nonces = Enumerable.Range(0, 2000).Select(_ => RegistryCallSigning.NewNonce()).ToList();

        nonces.Should().OnlyHaveUniqueItems();
        nonces.Should().OnlyContain(n => n.Length >= 16 && n.Length <= 64 && n.All(c => char.IsAsciiLetterOrDigit(c) || c == '+' || c == '/' || c == '='));
    }

    /// <summary>An empty part is refused rather than signed, so a missing target never produces a signature.</summary>
    [Fact]
    public void Payload_RefusesAnEmptyPart()
    {
        var act = () => RegistryCallSigning.Payload("GET", string.Empty, EmptyBody, "1", "n");

        act.Should().Throw<ArgumentException>();
    }
}
