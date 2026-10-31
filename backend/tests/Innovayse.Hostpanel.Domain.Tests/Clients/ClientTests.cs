using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.Clients;

/// <summary>Unit tests for the <see cref="Client"/> aggregate.</summary>
public class ClientTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Create should produce Active client with a domain event.</summary>
    [Fact]
    public void Create_ShouldReturnActiveClient_WithDomainEvent()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        Assert.Equal("user-1", client.UserId);
        Assert.Equal("John", client.FirstName);
        Assert.Equal(ClientStatus.Active, client.Status);
        Assert.Single(client.DomainEvents);
        var created = Assert.IsType<ClientCreatedEvent>(client.DomainEvents.Single());
        Assert.Equal(Now, created.OccurredAt);
    }

    /// <summary>Update should change profile fields.</summary>
    [Fact]
    public void Update_ShouldChangeProfileFields()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Update("Jane", "Smith", "Acme Corp", "+1-555-0100");

        Assert.Equal("Jane", client.FirstName);
        Assert.Equal("Smith", client.LastName);
        Assert.Equal("Acme Corp", client.CompanyName);
        Assert.Equal("+1-555-0100", client.Phone);
    }

    /// <summary>Suspend should change status to Suspended.</summary>
    [Fact]
    public void Suspend_ShouldSetStatusToSuspended()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Suspend(Now);

        Assert.Equal(ClientStatus.Suspended, client.Status);
    }

    /// <summary>
    /// Suspending an already suspended client throws <see cref="DomainRuleException"/> with
    /// <c>CLIENT_SUSPEND_NOT_ALLOWED</c>.
    /// </summary>
    [Fact]
    public void Suspend_WhenAlreadySuspended_ThrowsWithClientSuspendNotAllowed()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Suspend(Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.Suspend(Now));
        Assert.Equal(ClientsErrors.ClientSuspendNotAllowed, ex.Code);
    }

    /// <summary>Activate should restore a suspended client to Active.</summary>
    [Fact]
    public void Activate_WhenSuspended_ShouldSetStatusToActive()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Suspend(Now);
        client.Activate(Now);

        Assert.Equal(ClientStatus.Active, client.Status);
    }

    /// <summary>Activating an already active account should not throw (idempotent).</summary>
    [Fact]
    public void Activate_WhenAlreadyActive_ShouldNotThrow()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Activate(Now); // already Active, so this is a no-op
        Assert.Equal(ClientStatus.Active, client.Status);
    }

    /// <summary>Deactivate moves an active client to Inactive.</summary>
    [Fact]
    public void Deactivate_WhenActive_ShouldSetStatusToInactive()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        client.Deactivate(Now);

        Assert.Equal(ClientStatus.Inactive, client.Status);
    }

    /// <summary>A closed client cannot be moved to Inactive.</summary>
    [Fact]
    public void Deactivate_WhenClosed_ShouldThrow()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Close(Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.Deactivate(Now));

        Assert.Equal(ClientsErrors.ClientDeactivateNotAllowed, ex.Code);
    }

    /// <summary>AddContact should append a contact to the collection.</summary>
    [Fact]
    public void AddContact_ShouldAppendToContacts()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddContact(ContactOf("Jane", "Doe", "jane@example.com"));

        Assert.Single(client.Contacts);
        Assert.Equal("Jane", client.Contacts[0].FirstName);
        Assert.Equal("Doe", client.Contacts[0].LastName);
    }

    /// <summary>RemoveContact should remove the specified contact.</summary>
    [Fact]
    public void RemoveContact_ShouldRemoveFromContacts()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddContact(ContactOf("Jane", "Doe", "jane@example.com"));

        // Contact Id will be 0 since EF Core hasn't assigned a real Id.
        var contactId = client.Contacts[0].Id;
        client.RemoveContact(contactId);

        Assert.Empty(client.Contacts);
    }

    /// <summary>
    /// RemoveContact with an unknown id throws <see cref="DomainRuleException"/> with
    /// <c>CLIENT_CONTACT_NOT_FOUND</c>.
    /// </summary>
    [Fact]
    public void RemoveContact_WhenNotFound_ThrowsWithClientContactNotFound()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        var ex = Assert.Throws<DomainRuleException>(() => client.RemoveContact(999));
        Assert.Equal(ClientsErrors.ClientContactNotFound, ex.Code);
    }

    /// <summary>UpdateContact should update the specified contact's fields.</summary>
    [Fact]
    public void UpdateContact_ShouldUpdateFields()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddContact(ContactOf("Jane", "Doe", "jane@example.com"));

        var contactId = client.Contacts[0].Id;
        client.UpdateContact(
            contactId,
            new ContactDetails(
                "Janet", "Smith", "Acme", "janet@example.com", "+1-555-0100", ContactType.Technical,
                new PostalAddress("123 Main St", null, "Springfield", null, "00100", "us"),
                new ContactNotifications(false, true, false, true, false, true)));

        var contact = client.Contacts[0];
        Assert.Equal("Janet", contact.FirstName);
        Assert.Equal("Smith", contact.LastName);
        Assert.Equal("Acme", contact.CompanyName);
        Assert.Equal("janet@example.com", contact.Email);
        Assert.Equal(ContactType.Technical, contact.Type);
        Assert.Equal("US", contact.Country);
        Assert.False(contact.NotifyGeneral);
    }

    /// <summary>
    /// AddUser with the account owner's own id throws <see cref="DomainRuleException"/> with
    /// <c>CLIENT_USER_IS_OWNER</c>.
    /// </summary>
    [Fact]
    public void AddUser_WhenUserIsOwner_ThrowsWithClientUserIsOwner()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.AddUser("user-1", ClientPermission.None));
        Assert.Equal(ClientsErrors.ClientUserIsOwner, ex.Code);
    }

    /// <summary>
    /// AddUser with a user already linked throws <see cref="DomainRuleException"/> with
    /// <c>CLIENT_USER_LINK_DUPLICATE</c>.
    /// </summary>
    [Fact]
    public void AddUser_WhenAlreadyLinked_ThrowsWithClientUserLinkDuplicate()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddUser("user-2", ClientPermission.None);

        var ex = Assert.Throws<DomainRuleException>(() => client.AddUser("user-2", ClientPermission.None));
        Assert.Equal(ClientsErrors.ClientUserLinkDuplicate, ex.Code);
    }

    /// <summary>
    /// RemoveUser with an id that names no linked user throws <see cref="DomainRuleException"/> with
    /// <c>CLIENT_USER_NOT_LINKED</c>.
    /// </summary>
    [Fact]
    public void RemoveUser_WhenNotLinked_ThrowsWithClientUserNotLinked()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.RemoveUser("user-2"));
        Assert.Equal(ClientsErrors.ClientUserNotLinked, ex.Code);
    }

    /// <summary>
    /// TransferOwnership to the current owner throws <see cref="DomainRuleException"/> with
    /// <c>CLIENT_TRANSFER_SAME_OWNER</c>.
    /// </summary>
    [Fact]
    public void TransferOwnership_ToCurrentOwner_ThrowsWithClientTransferSameOwner()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.TransferOwnership("user-1"));
        Assert.Equal(ClientsErrors.ClientTransferSameOwner, ex.Code);
    }

    /// <summary>
    /// DeductCredit for more than the balance throws <see cref="DomainRuleException"/> with
    /// <c>CLIENT_CREDIT_INSUFFICIENT</c>.
    /// </summary>
    [Fact]
    public void DeductCredit_WhenExceedingBalance_ThrowsWithClientCreditInsufficient()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddCredit(10m);

        var ex = Assert.Throws<DomainRuleException>(() => client.DeductCredit(20m));
        Assert.Equal(ClientsErrors.ClientCreditInsufficient, ex.Code);
    }

    /// <summary>Closing an already-closed account throws <see cref="DomainRuleException"/> with <c>CLIENT_CLOSE_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Close_WhenAlreadyClosed_ThrowsWithClientCloseNotAllowed()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Close(Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.Close(Now));
        Assert.Equal(ClientsErrors.ClientCloseNotAllowed, ex.Code);
    }

    /// <summary>Activating a closed account throws <see cref="DomainRuleException"/> with <c>CLIENT_ACTIVATE_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Activate_WhenClosed_ThrowsWithClientActivateNotAllowed()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Close(Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.Activate(Now));
        Assert.Equal(ClientsErrors.ClientActivateNotAllowed, ex.Code);
    }

    /// <summary>A code is stored upper case whatever the caller typed, so "amd" and "AMD" are one currency.</summary>
    [Fact]
    public void SetCurrency_UpperCasesTheCode()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);

        client.SetCurrency("amd");

        Assert.Equal("AMD", client.Currency);
    }

    /// <summary>Anything but a three-letter code is refused: an invoice cannot be raised in "US$".</summary>
    /// <param name="code">A currency code that is not three letters.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("12!")]
    [InlineData("123")]
    [InlineData("U$D")]
    [InlineData(" eu")]
    public void SetCurrency_RejectsACodeThatIsNotThreeLetters(string code)
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);

        Assert.Throws<ArgumentException>(() => client.SetCurrency(code));
        Assert.Null(client.Currency);
    }

    /// <summary>With no credit balance the currency moves freely to another one.</summary>
    [Fact]
    public void SetCurrency_WithZeroCreditBalance_ChangesTheCurrency()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);
        client.SetCurrency("USD");

        client.SetCurrency("AMD");

        Assert.Equal("AMD", client.Currency);
    }

    /// <summary>
    /// A credit balance has no currency of its own, so a change to another currency is refused with
    /// <c>CLIENT_CURRENCY_LOCKED_BY_CREDIT</c> and leaves the currency and the balance as they were.
    /// </summary>
    [Fact]
    public void SetCurrency_WithCreditBalance_ThrowsWithClientCurrencyLockedByCredit()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);
        client.SetCurrency("USD");
        client.AddCredit(10m);

        var ex = Assert.Throws<DomainRuleException>(() => client.SetCurrency("AMD"));

        Assert.Equal(ClientsErrors.ClientCurrencyLockedByCredit, ex.Code);
        Assert.Equal("USD", client.Currency);
        Assert.Equal(10m, client.CreditBalance);
    }

    /// <summary>Naming the currency the client already has is not a change, so a balance does not refuse it.</summary>
    [Fact]
    public void SetCurrency_WithCreditBalance_AcceptsTheSameCurrency()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);
        client.SetCurrency("USD");
        client.AddCredit(10m);

        client.SetCurrency("usd");

        Assert.Equal("USD", client.Currency);
    }

    /// <summary>
    /// A client with a balance and no currency yet may be given its first currency: there is no
    /// previous currency whose value would change.
    /// </summary>
    [Fact]
    public void SetCurrency_FirstAssignmentWithCreditBalance_SetsTheCurrency()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);
        client.AddCredit(10m);

        client.SetCurrency("usd");

        Assert.Equal("USD", client.Currency);
        Assert.Equal(10m, client.CreditBalance);
    }

    /// <summary>Once the balance is used up to zero the currency can change again.</summary>
    [Fact]
    public void SetCurrency_AfterTheBalanceIsUsed_ChangesTheCurrency()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);
        client.SetCurrency("USD");
        client.AddCredit(10m);
        client.DeductCredit(10m);

        client.SetCurrency("AMD");

        Assert.Equal("AMD", client.Currency);
    }

    /// <summary>The other preferences no longer carry the currency, so writing them leaves it alone.</summary>
    [Fact]
    public void UpdatePreferences_DoesNotTouchTheCurrency()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com", Now);
        client.SetCurrency("USD");

        client.UpdatePreferences("bank_transfer", "billing@example.com", "note");

        Assert.Equal("USD", client.Currency);
        Assert.Equal("bank_transfer", client.PaymentMethod);
    }

    /// <summary>Backdate moves the signup date and leaves the account's state alone.</summary>
    [Fact]
    public void Backdate_ShouldMoveTheSignupDateOnly()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        var signedUp = new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero);

        client.Backdate(signedUp);

        Assert.Equal(signedUp, client.CreatedAt);
        Assert.Equal(ClientStatus.Active, client.Status);
    }

    /// <summary>Builds contact input with a billing type, an empty address and every notification on.</summary>
    /// <param name="firstName">The contact's first name.</param>
    /// <param name="lastName">The contact's last name.</param>
    /// <param name="email">The contact's email address.</param>
    /// <returns>The contact input.</returns>
    private static ContactDetails ContactOf(string firstName, string lastName, string email) => new(
        firstName,
        lastName,
        null,
        email,
        null,
        ContactType.Billing,
        new PostalAddress(null, null, null, null, null, null),
        new ContactNotifications(true, true, true, true, true, true));

    /// <summary>The registration details the caller passes are stored on the client.</summary>
    [Fact]
    public void Create_WithRegistrationInfo_StoresIt()
    {
        var client = Client.Create(
            "user-1", "John", "Doe", "john@example.com", Now,
            registration: new RegistrationInfo("203.0.113.9", "agent/1.0", "Desktop", "Linux", "Firefox 120"));

        Assert.Equal("203.0.113.9", client.RegistrationIp);
        Assert.Equal("agent/1.0", client.RegistrationUserAgent);
        Assert.Equal("Desktop", client.DeviceType);
        Assert.Equal("Linux", client.OperatingSystem);
        Assert.Equal("Firefox 120", client.Browser);
    }

    /// <summary>A client without a first name is refused, on creation and on update.</summary>
    /// <param name="firstName">A first name that is empty or only white space.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankFirstName_IsRefused(string firstName)
    {
        Assert.Throws<ArgumentException>(() => Client.Create("user-1", firstName, "Doe", "john@example.com", Now));

        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        Assert.Throws<ArgumentException>(() => client.Update(firstName, "Doe", null, null));
        Assert.Equal("John", client.FirstName);
    }

    /// <summary>A name or company longer than its column is refused instead of failing at the database.</summary>
    [Fact]
    public void ANameLongerThanItsColumn_IsRefused()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        Assert.Throws<ArgumentException>(() => client.Update(new string('a', 101), "Doe", null, null));
        Assert.Throws<ArgumentException>(() => client.Update("John", new string('a', 101), null, null));
        Assert.Throws<ArgumentException>(() => client.Update("John", "Doe", new string('a', 201), null));
    }

    /// <summary>The country is stored upper case whatever the caller typed.</summary>
    [Fact]
    public void UpdateAddress_StoresTheCountryUpperCase()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        client.UpdateAddress("1 Main St", null, "Yerevan", null, "0010", " am ");

        Assert.Equal("AM", client.Country);
    }

    /// <summary>Two contacts of one client cannot share an email address, whatever the case.</summary>
    [Fact]
    public void AddContact_WithAnAddressAnotherContactUses_Throws()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddContact(ContactOf("Jane", "Doe", "jane@example.com"));

        var ex = Assert.Throws<DomainRuleException>(() => client.AddContact(ContactOf("Janet", "Roe", "JANE@example.com")));

        Assert.Equal(ClientsErrors.ClientContactEmailInUse, ex.Code);
        Assert.Single(client.Contacts);
    }

    /// <summary>A contact keeps its own address when it is saved again; a clash with another contact is covered against the database.</summary>
    [Fact]
    public void UpdateContact_KeepingItsOwnAddress_IsAllowed()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddContact(ContactOf("Jane", "Doe", "jane@example.com"));
        var contactId = client.Contacts[0].Id;

        client.UpdateContact(contactId, ContactOf("Janet", "Doe", "JANE@example.com"));

        Assert.Equal("Janet", client.Contacts[0].FirstName);
    }

    /// <summary>A contact needs a name and an address; blank ones are refused.</summary>
    [Fact]
    public void AddContact_WithABlankNameOrAddress_Throws()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        Assert.Throws<ArgumentException>(() => client.AddContact(ContactOf(" ", "Doe", "jane@example.com")));
        Assert.Throws<ArgumentException>(() => client.AddContact(ContactOf("Jane", "", "jane@example.com")));
        Assert.Throws<ArgumentException>(() => client.AddContact(ContactOf("Jane", "Doe", " ")));
        Assert.Empty(client.Contacts);
    }

    /// <summary>The owner is not an additional user, so removing them is refused with its own code.</summary>
    [Fact]
    public void RemoveUser_WhenTheUserIsTheOwner_ThrowsWithClientOwnerCannotBeRemoved()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        var ex = Assert.Throws<DomainRuleException>(() => client.RemoveUser("user-1"));

        Assert.Equal(ClientsErrors.ClientOwnerCannotBeRemoved, ex.Code);
    }

    /// <summary>After a transfer the previous owner stays on the account as a user with every permission.</summary>
    [Fact]
    public void TransferOwnership_KeepsThePreviousOwnerWithAllPermissions()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);

        client.TransferOwnership("user-2");

        Assert.Equal("user-2", client.UserId);
        var previous = Assert.Single(client.Users);
        Assert.Equal("user-1", previous.UserId);
        Assert.Equal(ClientPermission.All, previous.Permissions);
    }

    /// <summary>A user who becomes the owner leaves the additional users, and the previous owner joins them.</summary>
    [Fact]
    public void TransferOwnership_MovesALinkedUserUpToOwner()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddUser("user-2", ClientPermission.ViewDomains);

        client.TransferOwnership("user-2");

        Assert.Equal("user-2", client.UserId);
        Assert.Equal(["user-1"], client.Users.Select(u => u.UserId));
    }

    /// <summary>A closed account's contacts and users are frozen: every change is refused with one code.</summary>
    [Fact]
    public void ContactAndUserChanges_OnAClosedClient_AreRefused()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.AddContact(ContactOf("Jane", "Doe", "jane@example.com"));
        client.AddUser("user-2", ClientPermission.None);
        var contactId = client.Contacts[0].Id;
        client.Close(Now);

        Action[] changes =
        [
            () => client.AddContact(ContactOf("Jack", "Doe", "jack@example.com")),
            () => client.UpdateContact(contactId, ContactOf("Janet", "Doe", "jane@example.com")),
            () => client.RemoveContact(contactId),
            () => client.AddUser("user-3", ClientPermission.None),
            () => client.RemoveUser("user-2"),
            () => client.UpdateUserPermissions("user-2", ClientPermission.All),
            () => client.TransferOwnership("user-2"),
        ];

        foreach (var change in changes)
        {
            var ex = Assert.Throws<DomainRuleException>(change);
            Assert.Equal(ClientsErrors.ClientRecordClosed, ex.Code);
        }

        Assert.Single(client.Contacts);
        Assert.Equal("user-1", client.UserId);
    }

    /// <summary>The default first name is the part of the address before the <c>@</c>.</summary>
    [Fact]
    public void FirstNameFromEmail_ReturnsTheLocalPart()
    {
        Assert.Equal("jane.doe", Client.FirstNameFromEmail("jane.doe@example.com"));
    }

    /// <summary>An address with no local part, or none at all, supplies no name.</summary>
    /// <param name="email">The address under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@example.com")]
    public void FirstNameFromEmail_WithNoLocalPart_ReturnsNull(string? email)
    {
        Assert.Null(Client.FirstNameFromEmail(email));
    }

    /// <summary>A local part longer than the first name column is cut to fit, so the client can still be created.</summary>
    [Fact]
    public void FirstNameFromEmail_WithALongLocalPart_FitsTheColumn()
    {
        var name = Client.FirstNameFromEmail(new string('a', 150) + "@example.com");

        Assert.Equal(100, name!.Length);
        Client.Create("user-1", name, "Doe", "x@example.com", Now);
    }

    /// <summary>Importing history is not a user change: a closed account takes the contacts it had in the source.</summary>
    [Fact]
    public void ImportContact_OnAClosedClient_AddsTheContact()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.Close(Now);

        var contact = client.ImportContact(ContactOf("Jane", "Doe", "jane@example.com"));

        Assert.Single(client.Contacts);
        Assert.Same(contact, client.Contacts[0]);
    }

    /// <summary>The import still keeps one contact per address, closed account or not.</summary>
    [Fact]
    public void ImportContact_WithAnAddressAnotherContactUses_Throws()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Now);
        client.ImportContact(ContactOf("Jane", "Doe", "jane@example.com"));

        Assert.Throws<DomainRuleException>(() => client.ImportContact(ContactOf("Janet", "Roe", "JANE@example.com")));
    }
}
