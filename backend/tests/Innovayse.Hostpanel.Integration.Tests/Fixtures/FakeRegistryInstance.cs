namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>An installation as the fake plugin registry knows it: the public key it registered with, its challenge and its state.</summary>
public sealed class FakeRegistryInstance
{
    /// <summary>Gets the instance id the registry gave.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the raw 32-byte public key.</summary>
    public required byte[] PublicKey { get; init; }

    /// <summary>Gets the public address the installation named.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>Gets the challenge the installation has to serve.</summary>
    public required string Challenge { get; init; }

    /// <summary>Gets or sets whether the installation passed the verification.</summary>
    public bool Verified { get; set; }

    /// <summary>Gets or sets whether an administrator revoked the installation.</summary>
    public bool Revoked { get; set; }

    /// <summary>Gets or sets how many times the registry tried to fetch the challenge.</summary>
    public int VerificationAttempts { get; set; }

    /// <summary>Gets the nonces this instance has used, for the replay check.</summary>
    public HashSet<string> Nonces { get; } = [];
}
