using Innovayse.Hostpanel.Application.Notifications.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Pure tests of <see cref="EmailIdempotencyKey"/>: a replayed message must map to the same key every
/// time, and distinct messages, templates and recipients must not. No host or database is used.
/// </summary>
public sealed class EmailIdempotencyKeyTests
{
    /// <summary>A key is a SHA-256 in lower-case hex: 64 characters of 0-9 and a-f.</summary>
    private const string KeyPattern = "^[0-9a-f]{64}$";

    /// <summary>A sample message the keys are built from.</summary>
    /// <param name="InvoiceId">The invoice the sample message is about.</param>
    /// <param name="Total">The amount it carries.</param>
    private sealed record SampleEvent(int InvoiceId, decimal Total);

    /// <summary>The same message and slug give the same key on every call, as a replay must.</summary>
    [Fact]
    public void ForMessage_SameMessageAndSlug_IsDeterministic()
    {
        var first = EmailIdempotencyKey.ForMessage(new SampleEvent(7, 10m), "invoice-created");
        var again = EmailIdempotencyKey.ForMessage(new SampleEvent(7, 10m), "invoice-created");

        again.Should().Be(first).And.MatchRegex(KeyPattern);
    }

    /// <summary>A different payload or a different template slug gives a different key.</summary>
    [Fact]
    public void ForMessage_DifferentPayloadOrSlug_DiffersKey()
    {
        var key = EmailIdempotencyKey.ForMessage(new SampleEvent(7, 10m), "invoice-created");

        EmailIdempotencyKey.ForMessage(new SampleEvent(8, 10m), "invoice-created").Should().NotBe(key);
        EmailIdempotencyKey.ForMessage(new SampleEvent(7, 10m), "invoice-reminder").Should().NotBe(key);
    }

    /// <summary>Parts are length-prefixed, so moving text across a part boundary changes the key.</summary>
    [Fact]
    public void ForParts_ShiftedBoundary_DiffersKey()
    {
        EmailIdempotencyKey.ForParts("ab", "c").Should().NotBe(EmailIdempotencyKey.ForParts("a", "bc"));
        EmailIdempotencyKey.ForParts("ab", "c").Should().Be(EmailIdempotencyKey.ForParts("ab", "c"));
    }

    /// <summary>The recipient key ignores case and surrounding spaces, and differs per address.</summary>
    [Fact]
    public void ForRecipient_NormalisesAddress_AndDiffersPerAddress()
    {
        var key = EmailIdempotencyKey.ForParts("42", "3");

        EmailIdempotencyKey.ForRecipient(key, " Ada@Example.test ")
            .Should().Be(EmailIdempotencyKey.ForRecipient(key, "ada@example.test"))
            .And.MatchRegex(KeyPattern);
        EmailIdempotencyKey.ForRecipient(key, "bob@example.test")
            .Should().NotBe(EmailIdempotencyKey.ForRecipient(key, "ada@example.test"));
    }
}
