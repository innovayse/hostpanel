namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// The client is mailed the credit note when it is issued, once, at their address with the number and the amount in it; a note
/// that is refused sends nothing, and the credit history line of the note sends no second mail of its own.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreditNoteIssuedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Issuing a note mails the client one message that names the note and the amount, and no balance mail goes with it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Issue_MailsTheClientTheNoteOnce()
    {
        var sender = new FakeEmailSender();
        var host = BankTransferTestSeeds.HostWithMail(factory, sender);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (_, clientId, _, email) = await BillingTestSeeds.RegisterClientAsync(factory, host, "cn-mail");
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(host, clientId, price: 100m, taxRate: 20m);
        var noteId = 0;

        await BankTransferTestSeeds.RunTrackedAsync(host, async () => noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 50m));

        var number = (await CreditNoteTestSeeds.NoteAsync(host, noteId)).Number!;
        var mail = sender.Sent.Where(m => m.To == email && m.Subject.Contains(number)).Should().ContainSingle().Subject;
        mail.Body.Should().Contain(number).And.Contain("60");
        sender.Sent.Where(m => m.To == email && m.Subject.Contains("credit balance", StringComparison.OrdinalIgnoreCase)).Should().BeEmpty();
    }

    /// <summary>A note that is refused sends nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARefusedNote_MailsNothing()
    {
        var sender = new FakeEmailSender();
        var host = BankTransferTestSeeds.HostWithMail(factory, sender);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (_, clientId, _, email) = await BillingTestSeeds.RegisterClientAsync(factory, host, "cn-mail-none");
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(host, clientId, price: 10m);

        await BankTransferTestSeeds.RunTrackedAsync(host, async () => await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 10.01m));

        sender.Sent.Where(m => m.To == email && m.Subject.Contains("CN-")).Should().BeEmpty();
    }
}
