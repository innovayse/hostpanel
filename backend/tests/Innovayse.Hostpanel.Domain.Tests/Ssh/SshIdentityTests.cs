using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Domain.Tests.Ssh;

/// <summary>A generated management identity never shows its private key when it is formatted.</summary>
public sealed class SshIdentityTests
{
    /// <summary>A throwaway public key; <see cref="SshPublicKeyTests"/> pins its fingerprint.</summary>
    private const string PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b user@host";

    /// <summary>The fingerprint of <see cref="PublicKey"/>, as <c>ssh-keygen -lf</c> prints it.</summary>
    private const string Fingerprint = "SHA256:xK0G2nacQ5l2jswa38Dl9jmRCsESwmesIiz26XjE5+o";

    /// <summary>A private key body that must never appear in any formatted output.</summary>
    private const string PrivateKeyBody = "b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZWQyNTUx";

    /// <summary>The private key in OpenSSH format.</summary>
    private static readonly string PrivateKey =
        $"-----BEGIN OPENSSH PRIVATE KEY-----\n{PrivateKeyBody}\n-----END OPENSSH PRIVATE KEY-----\n";

    /// <summary><c>ToString</c> carries neither the private key nor any part of it.</summary>
    [Fact]
    public void ToString_DoesNotContainThePrivateKey()
    {
        var text = new SshIdentity(PrivateKey, PublicKey).ToString();

        text.Should().NotContain(PrivateKeyBody).And.NotContain("PRIVATE KEY").And.NotContain("PrivateKey");
    }

    /// <summary>String interpolation, the way a log line or an exception message formats a value, shows no private key.</summary>
    [Fact]
    public void Interpolation_DoesNotContainThePrivateKey()
    {
        var identity = new SshIdentity(PrivateKey, PublicKey);

        $"identity: {identity}".Should().NotContain(PrivateKeyBody).And.NotContain("PRIVATE KEY");
    }

    /// <summary>The identity is described by the fingerprint of its public key, which is safe to log.</summary>
    [Fact]
    public void ToString_DescribesTheIdentityByItsFingerprint()
    {
        new SshIdentity(PrivateKey, PublicKey).ToString().Should().Be($"SshIdentity {{ PublicKeyFingerprint = {Fingerprint} }}");
    }

    /// <summary>A public key that does not parse has no fingerprint, so the description says so and still hides the private key.</summary>
    [Fact]
    public void ToString_UnparsablePublicKey_ReportsUnknownFingerprint()
    {
        new SshIdentity(PrivateKey, "not a key").ToString().Should().Be("SshIdentity { PublicKeyFingerprint = unknown }");
    }

    /// <summary>Equality is still value equality over both keys; only the text form changed.</summary>
    [Fact]
    public void Equality_ComparesBothKeys()
    {
        var identity = new SshIdentity(PrivateKey, PublicKey);

        identity.Should().Be(new SshIdentity(PrivateKey, PublicKey));
        identity.Should().NotBe(new SshIdentity(PrivateKey + "x", PublicKey));
    }
}
