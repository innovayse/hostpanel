using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>The registration state machine of this installation: what each change leaves behind, and when the next attempt is due.</summary>
public sealed class RegistryInstanceRecordTests
{
    /// <summary>A moment the tests count from.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Makes a record with a fixed pair.</summary>
    /// <returns>A record that is not registered yet.</returns>
    private static RegistryInstanceRecord New()
    {
        return RegistryInstanceRecord.Create(new RegistryInstanceKeyPair("pub", "priv"), "https://panel.example.com");
    }

    /// <summary>A new record is not registered, has no id and is due at once.</summary>
    [Fact]
    public void ANewRecord_IsNotRegistered_AndDueAtOnce()
    {
        var record = New();

        record.State.Should().Be(RegistryInstanceState.NotRegistered);
        record.InstanceId.Should().BeNull();
        record.IsDue(Now).Should().BeTrue();
    }

    /// <summary>A registration is pending with its id and challenge, and clears an earlier failure.</summary>
    [Fact]
    public void Register_MakesTheRecordPending_AndClearsTheFailure()
    {
        var record = New();
        record.Fail("PLUGIN_REGISTRY_UNAVAILABLE", Now);
        var id = Guid.NewGuid();

        record.Register(new RegistryInstanceRegistration(id, "challenge"));

        record.State.Should().Be(RegistryInstanceState.Pending);
        record.InstanceId.Should().Be(id);
        record.Challenge.Should().Be("challenge");
        record.ErrorCode.Should().BeNull();
        record.Failures.Should().Be(0);
        record.IsDue(Now).Should().BeTrue();
    }

    /// <summary>A failure sets the code, counts, and waits the back-off before the next attempt is due.</summary>
    [Fact]
    public void Fail_SetsTheCode_AndWaitsTheBackoff()
    {
        var record = New();

        record.Fail("PLUGIN_REGISTRY_UNAVAILABLE", Now);

        record.State.Should().Be(RegistryInstanceState.Error);
        record.ErrorCode.Should().Be("PLUGIN_REGISTRY_UNAVAILABLE");
        record.IsDue(Now + TimeSpan.FromSeconds(29)).Should().BeFalse();
        record.IsDue(Now + TimeSpan.FromSeconds(30)).Should().BeTrue();
        record.Fail("PLUGIN_REGISTRY_UNAVAILABLE", Now);
        record.Failures.Should().Be(2);
        record.IsDue(Now + TimeSpan.FromSeconds(59)).Should().BeFalse();
        record.IsDue(Now + TimeSpan.FromSeconds(60)).Should().BeTrue();
    }

    /// <summary>Verification clears the failure and the pause and stamps the moment.</summary>
    [Fact]
    public void Verify_ClearsTheFailure_AndStampsTheMoment()
    {
        var record = New();
        record.Register(new RegistryInstanceRegistration(Guid.NewGuid(), "c"));
        record.Fail("PLUGIN_REGISTRY_UNAVAILABLE", Now);

        record.Verify(Now);

        record.State.Should().Be(RegistryInstanceState.Verified);
        record.ErrorCode.Should().BeNull();
        record.Failures.Should().Be(0);
        record.VerifiedAt.Should().Be(Now);
        record.NextAttemptAt.Should().BeNull();
    }

    /// <summary>A revocation is final for the automatic job: no failure code, no pause.</summary>
    [Fact]
    public void Revoke_LeavesNoFailureAndNoPause()
    {
        var record = New();
        record.Register(new RegistryInstanceRegistration(Guid.NewGuid(), "c"));
        record.Verify(Now);

        record.Revoke();

        record.State.Should().Be(RegistryInstanceState.Revoked);
        record.ErrorCode.Should().BeNull();
        record.NextAttemptAt.Should().BeNull();
    }

    /// <summary>An instance the registry no longer treats as verified goes back to pending, with the code, and is due at once.</summary>
    [Fact]
    public void Unverify_GoesBackToPending_AndIsDueAtOnce()
    {
        var record = New();
        record.Register(new RegistryInstanceRegistration(Guid.NewGuid(), "c"));
        record.Verify(Now);

        record.Unverify("PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED");

        record.State.Should().Be(RegistryInstanceState.Pending);
        record.ErrorCode.Should().Be("PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED");
        record.VerifiedAt.Should().BeNull();
        record.IsDue(Now).Should().BeTrue();
    }

    /// <summary>A restart forgets the registration and takes the new pair and the address.</summary>
    [Fact]
    public void Restart_ForgetsTheRegistration_AndTakesTheNewPair()
    {
        var record = New();
        record.Register(new RegistryInstanceRegistration(Guid.NewGuid(), "c"));
        record.Fail("PLUGIN_REGISTRY_UNAVAILABLE", Now);

        record.Restart(new RegistryInstanceKeyPair("pub2", "priv2"), "https://other.example.com");

        record.PublicKey.Should().Be("pub2");
        record.PrivateKey.Should().Be("priv2");
        record.BaseUrl.Should().Be("https://other.example.com");
        record.InstanceId.Should().BeNull();
        record.Challenge.Should().BeNull();
        record.State.Should().Be(RegistryInstanceState.NotRegistered);
        record.Failures.Should().Be(0);
        record.IsDue(Now).Should().BeTrue();
    }

    /// <summary>Clearing the pause makes the next attempt due without touching the failure.</summary>
    [Fact]
    public void ClearPause_MakesTheNextAttemptDue()
    {
        var record = New();
        record.Fail("PLUGIN_REGISTRY_UNAVAILABLE", Now);

        record.ClearPause();

        record.IsDue(Now).Should().BeTrue();
        record.ErrorCode.Should().Be("PLUGIN_REGISTRY_UNAVAILABLE");
    }
}
