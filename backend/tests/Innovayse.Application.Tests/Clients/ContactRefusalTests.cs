namespace Innovayse.Application.Tests.Clients;

using Innovayse.Application.Clients.Commands.RemoveContact;
using Innovayse.Application.Clients.Commands.UpdateContact;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Proves the contact routes refuse another account's contact and a contact that does not exist
/// with one answer, and that the answer names no id.
/// <para>
/// This is the property worth pinning rather than the status alone. A cross-account sweep of the
/// client-facing API found no IDOR anywhere, but <c>/api/clients/me/contacts/{contactId}</c> was
/// the one resource that refused with 400 / <c>INVALID_OPERATION</c> and a hardcoded English
/// <c>Contact {id} not found.</c> raised by the aggregate. The two refusals happened to be
/// identical, so it was never an oracle -- and that is exactly the kind of accident somebody
/// "improves" away by making one message more helpful than the other. These tests fail if they do.
/// </para>
/// </summary>
public sealed class ContactRefusalTests
{
    /// <summary>The caller's own client id, as the controller resolves it from the profile.</summary>
    private const int CallerClientId = 1;

    /// <summary>
    /// A contact id that is not on the caller's account. In the live sweep this was client 2's
    /// own contact; the handler never sees whose it is, only that its own aggregate has no such
    /// contact, which is the whole reason the two cases cannot be told apart.
    /// </summary>
    private const int StrangersContactId = 2;

    /// <summary>A contact id no account holds.</summary>
    private const int MissingContactId = 99999;

    /// <summary>
    /// Builds the caller's client with one contact of its own, so the refusals below come from
    /// the id not matching rather than from the account having no contacts at all.
    /// </summary>
    /// <returns>The caller's client aggregate.</returns>
    private static Client CallerClient()
    {
        var client = Client.Create("user-caller", "Jane", "Doe", "jane@example.com");
        client.AddContact(
            "Own", "Contact", null, "own@example.com", null, ContactType.General,
            null, null, null, null, null, null,
            true, true, true, true, true, true);
        return client;
    }

    /// <summary>
    /// Builds an <see cref="UpdateContactHandler"/> over the caller's own account.
    /// </summary>
    /// <returns>The handler under test.</returns>
    private static UpdateContactHandler UpdateHandler()
    {
        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByIdAsync(CallerClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerClient());

        // Strict: a refusal must happen before anything is written, so any save is a failure.
        return new UpdateContactHandler(clients.Object, new Mock<IUnitOfWork>(MockBehavior.Strict).Object);
    }

    /// <summary>
    /// Builds a <see cref="RemoveContactHandler"/> over the caller's own account.
    /// </summary>
    /// <returns>The handler under test.</returns>
    private static RemoveContactHandler RemoveHandler()
    {
        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByIdAsync(CallerClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerClient());

        return new RemoveContactHandler(clients.Object, new Mock<IUnitOfWork>(MockBehavior.Strict).Object);
    }

    /// <summary>Builds an update command for the given contact id. The fields are irrelevant.</summary>
    /// <param name="contactId">The contact id being probed.</param>
    /// <returns>The command.</returns>
    private static UpdateContactCommand UpdateOf(int contactId) => new(
        ClientId: CallerClientId,
        ContactId: contactId,
        FirstName: "Probe",
        LastName: "Probe",
        CompanyName: null,
        Email: "probe@example.com",
        Phone: null,
        Type: ContactType.General,
        Street: null,
        Address2: null,
        City: null,
        State: null,
        PostCode: null,
        Country: null,
        NotifyGeneral: true,
        NotifyInvoice: true,
        NotifySupport: true,
        NotifyProduct: true,
        NotifyDomain: true,
        NotifyAffiliate: true);

    /// <summary>
    /// The property this file exists for: updating another account's contact and updating one
    /// that does not exist produce the same exception type and the same sentence, so the two
    /// responses are identical byte for byte.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task UpdateContact_StrangersAndMissing_RefuseIdenticallyAsync()
    {
        var strangers = await Assert.ThrowsAsync<MyContactNotFoundException>(
            () => UpdateHandler().HandleAsync(UpdateOf(StrangersContactId), CancellationToken.None));

        var missing = await Assert.ThrowsAsync<MyContactNotFoundException>(
            () => UpdateHandler().HandleAsync(UpdateOf(MissingContactId), CancellationToken.None));

        Assert.Equal(MyContactNotFoundException.PublicMessage, strangers.Message);
        Assert.Equal(strangers.Message, missing.Message);
    }

    /// <summary>The same, for the delete route.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RemoveContact_StrangersAndMissing_RefuseIdenticallyAsync()
    {
        var strangers = await Assert.ThrowsAsync<MyContactNotFoundException>(
            () => RemoveHandler().HandleAsync(new RemoveContactCommand(CallerClientId, StrangersContactId), CancellationToken.None));

        var missing = await Assert.ThrowsAsync<MyContactNotFoundException>(
            () => RemoveHandler().HandleAsync(new RemoveContactCommand(CallerClientId, MissingContactId), CancellationToken.None));

        Assert.Equal(MyContactNotFoundException.PublicMessage, strangers.Message);
        Assert.Equal(strangers.Message, missing.Message);
    }

    /// <summary>
    /// The refusal must not echo the probed id back. That is what keeps the two answers above
    /// equal no matter which id was asked for.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task UpdateContact_WhenRefusing_DoesNotNameTheContactInTheMessageAsync()
    {
        var refusal = await Assert.ThrowsAsync<MyContactNotFoundException>(
            () => UpdateHandler().HandleAsync(UpdateOf(StrangersContactId), CancellationToken.None));

        Assert.DoesNotContain(StrangersContactId.ToString(), refusal.Message, StringComparison.Ordinal);
        Assert.Equal(StrangersContactId, refusal.ContactId);
    }

    /// <summary>
    /// The refusal carries the typed code, not the unclassified 400 bin it used to fall into.
    /// </summary>
    [Fact]
    public void RefusalCarriesItsOwnCode()
    {
        Assert.Equal("MY_CONTACT_NOT_FOUND", MyContactNotFoundException.Code);
        Assert.Equal("MyContactNotFound", MyContactNotFoundException.MessageKey);
    }
}
