using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients;

/// <summary>
/// Aggregate root for a client account.
/// Linked to an ASP.NET Core Identity user via <see cref="UserId"/>.
/// Contains the billing address inline and a collection of additional contacts.
/// Stored in the <c>Clients</c> table.
/// </summary>
public sealed class Client : BaseEntity, IAggregateRoot
{
    /// <summary>Length of an ISO 4217 alpha code, the only shape <see cref="Currency"/> is stored in.</summary>
    private const int CurrencyCodeLength = 3;

    /// <summary>Longest first or last name the stored column holds.</summary>
    private const int NameMaxLength = 100;

    /// <summary>Longest company name the stored column holds.</summary>
    private const int CompanyNameMaxLength = 200;

    /// <summary>
    /// Longest name of the other system an external account link points into, in characters; the
    /// <c>Clients.ExternalAccountProvider</c> column is this wide.
    /// </summary>
    public const int ExternalAccountProviderMaxLength = 64;

    /// <summary>
    /// Longest identifier the other system may give the account, in characters; the
    /// <c>Clients.ExternalAccountId</c> column is this wide.
    /// </summary>
    public const int ExternalAccountIdMaxLength = 256;

    /// <summary>Internal mutable contacts list.</summary>
    private readonly List<Contact> _contacts = [];

    /// <summary>Internal mutable list of non-owner users linked to this client.</summary>
    private readonly List<ClientUser> _users = [];

    /// <summary>Gets the ASP.NET Core Identity user ID (FK to AspNetUsers).</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Gets the client's first name.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Gets the client's last name.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>Gets the company name, or <see langword="null"/> for individual clients.</summary>
    public string? CompanyName { get; private set; }

    /// <summary>Gets the client's phone number.</summary>
    public string? Phone { get; private set; }

    /// <summary>Gets the current lifecycle status of the account.</summary>
    public ClientStatus Status { get; private set; }

    /// <summary>Gets the billing address street line.</summary>
    public string? Street { get; private set; }

    /// <summary>Gets the billing address city.</summary>
    public string? City { get; private set; }

    /// <summary>Gets the billing address state or region.</summary>
    public string? State { get; private set; }

    /// <summary>Gets the billing address postcode.</summary>
    public string? PostCode { get; private set; }

    /// <summary>Gets the billing address ISO 3166-1 alpha-2 country code (e.g. "US", "AM").</summary>
    public string? Country { get; private set; }

    /// <summary>Gets the second billing address line.</summary>
    public string? Address2 { get; private set; }

    /// <summary>Gets the ISO 4217 currency code (e.g. "USD", "EUR", "AMD").</summary>
    public string? Currency { get; private set; }

    /// <summary>Gets the preferred payment method label.</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>
    /// Gets the card gateway customer ID this client's saved cards are attached to, or
    /// <see langword="null"/> until the client has added their first card.
    /// </summary>
    public string? CardGatewayCustomerId { get; private set; }

    /// <summary>
    /// Gets the name of the other system this client is linked to (for example a CRM), or
    /// <see langword="null"/> when the client has no external account link. Set and cleared
    /// together with <see cref="ExternalAccountId"/>, so one is never present without the other.
    /// </summary>
    public string? ExternalAccountProvider { get; private set; }

    /// <summary>
    /// Gets the identifier the system named by <see cref="ExternalAccountProvider"/> gives this client,
    /// or <see langword="null"/> when the client has no external account link.
    /// </summary>
    public string? ExternalAccountId { get; private set; }

    /// <summary>Gets the billing contact reference.</summary>
    public string? BillingContact { get; private set; }

    /// <summary>Gets internal admin-only notes.</summary>
    public string? AdminNotes { get; private set; }

    // ── Email notification preferences ──────────────────────────────────────

    /// <summary>Gets whether general account emails are enabled.</summary>
    public bool NotifyGeneral { get; private set; } = true;

    /// <summary>Gets whether invoice and billing emails are enabled.</summary>
    public bool NotifyInvoice { get; private set; } = true;

    /// <summary>Gets whether support ticket emails are enabled.</summary>
    public bool NotifySupport { get; private set; } = true;

    /// <summary>Gets whether product lifecycle emails are enabled.</summary>
    public bool NotifyProduct { get; private set; } = true;

    /// <summary>Gets whether domain registration emails are enabled.</summary>
    public bool NotifyDomain { get; private set; } = true;

    /// <summary>Gets whether affiliate program emails are enabled.</summary>
    public bool NotifyAffiliate { get; private set; } = true;

    // ── Billing settings ────────────────────────────────────────────────────

    /// <summary>Gets whether payment reminder emails for unpaid invoices are sent to this client.</summary>
    public bool OverdueNotices { get; private set; } = true;

    /// <summary>Gets whether this client is tax exempt: no tax rate can be set on their invoices.</summary>
    public bool TaxExempt { get; private set; }

    /// <summary>Gets whether invoices are generated separately per service.</summary>
    public bool SeparateInvoices { get; private set; }

    /// <summary>Gets whether credit card processing is disabled: no card payment or saved card charge is taken from this client.</summary>
    public bool DisableCcProcessing { get; private set; }

    /// <summary>Gets whether this client has opted in to marketing emails.</summary>
    public bool MarketingOptIn { get; private set; }

    /// <summary>Gets whether status update emails are sent to this client.</summary>
    public bool StatusUpdate { get; private set; } = true;

    /// <summary>Gets whether single sign-on is allowed for this client.</summary>
    public bool AllowSso { get; private set; } = true;

    /// <summary>Gets the client's credit balance available for future invoices.</summary>
    public decimal CreditBalance { get; private set; }

    /// <summary>Gets the UTC timestamp when the client account was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the IP address captured during self-registration, or <see langword="null"/> for admin-created clients.</summary>
    public string? RegistrationIp { get; private set; }

    /// <summary>Gets the raw browser/device user-agent captured during self-registration, or <see langword="null"/> for admin-created clients.</summary>
    public string? RegistrationUserAgent { get; private set; }

    /// <summary>Gets the device type (Desktop, Mobile, Tablet) parsed from the user-agent, or <see langword="null"/> for admin-created clients.</summary>
    public string? DeviceType { get; private set; }

    /// <summary>Gets the operating system parsed from the user-agent, or <see langword="null"/> for admin-created clients.</summary>
    public string? OperatingSystem { get; private set; }

    /// <summary>Gets the browser name and version parsed from the user-agent, or <see langword="null"/> for admin-created clients.</summary>
    public string? Browser { get; private set; }

    /// <summary>Gets the additional contacts linked to this client.</summary>
    public IReadOnlyList<Contact> Contacts => _contacts.AsReadOnly();

    /// <summary>Gets the non-owner users linked to this client account.</summary>
    public IReadOnlyList<ClientUser> Users => _users.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Client() { }

    /// <summary>
    /// Creates a new active client account and raises <see cref="ClientCreatedEvent"/>.
    /// </summary>
    /// <param name="userId">The Identity user ID to link this client to.</param>
    /// <param name="firstName">The client's first name.</param>
    /// <param name="lastName">The client's last name.</param>
    /// <param name="email">The client's email address (used for the domain event).</param>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <param name="companyName">Optional company name.</param>
    /// <param name="registration">What was captured at self-registration; <see langword="null"/> for admin-created clients.</param>
    /// <returns>A new, unpersisted <see cref="Client"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the first name is blank or a name or the company name is longer than its column.</exception>
    public static Client Create(string userId, string firstName, string lastName, string email, DateTimeOffset now, string? companyName = null, RegistrationInfo? registration = null)
    {
        var client = new Client
        {
            UserId = userId,
            Status = ClientStatus.Active,
            RegistrationIp = registration?.IpAddress,
            RegistrationUserAgent = registration?.UserAgent,
            DeviceType = registration?.DeviceType,
            OperatingSystem = registration?.OperatingSystem,
            Browser = registration?.Browser,
        };
        client.SetName(firstName, lastName, companyName);

        // ClientId is 0 here: EF Core assigns the real Id after SaveChanges.
        // The Unit of Work dispatches domain events post-save, so handlers receive the correct Id.
        client.AddDomainEvent(() => new ClientCreatedEvent(client.Id, userId, email, now));

        return client;
    }

    /// <summary>
    /// The first name a client gets when whoever creates the record has no name for them: the part of
    /// the email address before the <c>@</c>, cut to the column's length. The same fallback the
    /// account store applies to an account created without a name, so the two agree.
    /// </summary>
    /// <param name="email">The client's email address.</param>
    /// <returns>The name, or <see langword="null"/> when the address has no local part to use.</returns>
    public static string? FirstNameFromEmail(string? email)
    {
        var localPart = email?.Split('@')[0].Trim();
        if (string.IsNullOrEmpty(localPart))
        {
            return null;
        }

        return localPart.Length > NameMaxLength ? localPart[..NameMaxLength] : localPart;
    }

    /// <summary>Moves the signup date to the moment the customer really joined, for accounts seeded from history.</summary>
    /// <param name="createdAt">The moment the account was created (UTC).</param>
    public void Backdate(DateTimeOffset createdAt) => CreatedAt = createdAt;

    /// <summary>
    /// Updates the client's profile information.
    /// </summary>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    /// <param name="companyName">New company name (null to clear).</param>
    /// <param name="phone">New phone number (null to clear).</param>
    /// <exception cref="ArgumentException">Thrown when the first name is blank or a name or the company name is longer than its column.</exception>
    public void Update(string firstName, string lastName, string? companyName, string? phone)
    {
        SetName(firstName, lastName, companyName);
        Phone = phone;
    }

    /// <summary>
    /// Updates the client's billing address.
    /// </summary>
    /// <param name="street">Street line.</param>
    /// <param name="address2">Second address line.</param>
    /// <param name="city">City, or null to clear it.</param>
    /// <param name="state">State or region.</param>
    /// <param name="postCode">Postcode or ZIP.</param>
    /// <param name="country">ISO 3166-1 alpha-2 country code; stored upper case.</param>
    public void UpdateAddress(string? street, string? address2, string? city, string? state, string? postCode, string? country)
    {
        Street = street;
        Address2 = address2;
        City = city;
        State = state;
        PostCode = postCode;
        Country = CountryCode.Normalize(country);
    }

    /// <summary>
    /// Updates the client's billing preferences.
    /// </summary>
    /// <remarks>
    /// The currency is deliberately not one of these. It is set through <see cref="SetCurrency"/>,
    /// because whether it may change at all depends on billing history the handler has to ask
    /// for first; folding it in here let every save of the preferences form rewrite it unchecked.
    /// </remarks>
    /// <param name="paymentMethod">Preferred payment method label.</param>
    /// <param name="billingContact">Billing contact reference.</param>
    /// <param name="adminNotes">Internal admin notes.</param>
    public void UpdatePreferences(string? paymentMethod, string? billingContact, string? adminNotes)
    {
        PaymentMethod = paymentMethod;
        BillingContact = billingContact;
        AdminNotes = adminNotes;
    }

    /// <summary>Sets the currency the client is billed in.</summary>
    /// <remarks>
    /// Whether this is allowed is not the client's to know: it depends on whether any invoice
    /// exists, which lives elsewhere. The handler checks and refuses; this method only records,
    /// in the same shape <c>Invoice</c> stores it: three letters, upper case.
    /// </remarks>
    /// <param name="code">ISO 4217 alpha code, normalised to upper case.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is not three characters.</exception>
    public void SetCurrency(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != CurrencyCodeLength)
        {
            throw new ArgumentException("A client's currency is a three-letter ISO 4217 code.", nameof(code));
        }

        Currency = code.ToUpperInvariant();
    }

    /// <summary>
    /// Records the card gateway customer this client's saved cards attach to.
    /// </summary>
    /// <param name="customerId">The card gateway customer ID.</param>
    /// <remarks>
    /// Set once, the first time the client adds a card: a client is never expected to move
    /// to a different card gateway customer afterward, so this does not need to support clearing it.
    /// </remarks>
    public void SetCardGatewayCustomerId(string customerId)
    {
        CardGatewayCustomerId = customerId;
    }

    /// <summary>
    /// Links this client to an account in another system: this client is account
    /// <paramref name="accountId"/> in <paramref name="provider"/>. Replaces any earlier link.
    /// </summary>
    /// <remarks>
    /// Both values are written here and only here, so a link is always whole; hostpanel stores the
    /// pair as given (trimmed) and attaches no meaning to either value.
    /// </remarks>
    /// <param name="provider">The other system's name; not blank, trimmed before it is stored.</param>
    /// <param name="accountId">The identifier that system gives the client; not blank, trimmed before it is stored.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when either value is blank or longer than its column after trimming.
    /// </exception>
    public void SetExternalAccount(string provider, string accountId)
    {
        var trimmedProvider = provider?.Trim();
        if (string.IsNullOrEmpty(trimmedProvider) || trimmedProvider.Length > ExternalAccountProviderMaxLength)
        {
            throw new ArgumentException(
                $"An external account link names its system in at most {ExternalAccountProviderMaxLength} characters.", nameof(provider));
        }

        var trimmedAccountId = accountId?.Trim();
        if (string.IsNullOrEmpty(trimmedAccountId) || trimmedAccountId.Length > ExternalAccountIdMaxLength)
        {
            throw new ArgumentException(
                $"An external account link carries an account id of at most {ExternalAccountIdMaxLength} characters.", nameof(accountId));
        }

        ExternalAccountProvider = trimmedProvider;
        ExternalAccountId = trimmedAccountId;
    }

    /// <summary>
    /// Removes the link to an account in another system, clearing both halves at once.
    /// Does nothing when the client has no link.
    /// </summary>
    public void ClearExternalAccount()
    {
        ExternalAccountProvider = null;
        ExternalAccountId = null;
    }

    /// <summary>
    /// Updates the client's email notification preferences.
    /// </summary>
    /// <param name="general">General account emails.</param>
    /// <param name="invoice">Invoice and billing emails.</param>
    /// <param name="support">Support ticket emails.</param>
    /// <param name="product">Product lifecycle emails.</param>
    /// <param name="domain">Domain registration emails.</param>
    /// <param name="affiliate">Affiliate program emails.</param>
    public void UpdateNotifications(bool general, bool invoice, bool support, bool product, bool domain, bool affiliate)
    {
        NotifyGeneral = general;
        NotifyInvoice = invoice;
        NotifySupport = support;
        NotifyProduct = product;
        NotifyDomain = domain;
        NotifyAffiliate = affiliate;
    }

    /// <summary>
    /// Updates the client's billing settings.
    /// </summary>
    /// <param name="overdueNotices">Send payment reminder emails for unpaid invoices.</param>
    /// <param name="taxExempt">Tax exempt flag.</param>
    /// <param name="separateInvoices">Separate invoices per service.</param>
    /// <param name="disableCcProcessing">Refuse card payments and saved card charges.</param>
    /// <param name="marketingOptIn">Marketing emails opt-in.</param>
    /// <param name="statusUpdate">Send status update emails.</param>
    /// <param name="allowSso">Allow single sign-on into the hosting panels of this client's services.</param>
    public void UpdateSettings(
        bool overdueNotices, bool taxExempt, bool separateInvoices,
        bool disableCcProcessing, bool marketingOptIn, bool statusUpdate, bool allowSso)
    {
        OverdueNotices = overdueNotices;
        TaxExempt = taxExempt;
        SeparateInvoices = separateInvoices;
        DisableCcProcessing = disableCcProcessing;
        MarketingOptIn = marketingOptIn;
        StatusUpdate = statusUpdate;
        AllowSso = allowSso;
    }

    /// <summary>
    /// Adds a new additional contact to this client.
    /// </summary>
    /// <param name="details">What was entered about the contact.</param>
    /// <returns>The new contact, whose <see cref="BaseEntity.Id"/> is assigned when the client is saved.</returns>
    /// <exception cref="DomainRuleException">Thrown when the account is closed or another contact already uses the email address.</exception>
    /// <exception cref="ArgumentException">Thrown when a name or the email address is blank, or a value is longer than its column.</exception>
    public Contact AddContact(ContactDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        EnsureNotClosed();
        return AppendContact(details);
    }

    /// <summary>
    /// Records a contact the account already had in the system it is being migrated from.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="AddContact"/> this does not refuse a closed account. Closing stops the
    /// people on an account from being changed by anyone using it; importing history is not that:
    /// a client that was closed in the source still has the contacts it had, and the import must
    /// be able to bring them across. Only the migration importers call it.
    /// </remarks>
    /// <param name="details">What the source holds about the contact.</param>
    /// <returns>The new contact, whose <see cref="BaseEntity.Id"/> is assigned when the client is saved.</returns>
    /// <exception cref="DomainRuleException">Thrown when another contact already uses the email address.</exception>
    /// <exception cref="ArgumentException">Thrown when a name or the email address is blank, or a value is longer than its column.</exception>
    public Contact ImportContact(ContactDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return AppendContact(details);
    }

    /// <summary>
    /// Removes a contact by ID. Throws if not found.
    /// </summary>
    /// <param name="contactId">The contact's primary key.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is closed or the contact is not found.</exception>
    public void RemoveContact(int contactId)
    {
        EnsureNotClosed();
        _contacts.Remove(FindContact(contactId));
    }

    /// <summary>
    /// Updates an existing contact's fields. Throws if the contact is not found.
    /// </summary>
    /// <param name="contactId">The contact's primary key.</param>
    /// <param name="details">The new values for every editable field.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown when the account is closed, the contact is not found, or another contact already uses the email address.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a name or the email address is blank, or a value is longer than its column.</exception>
    public void UpdateContact(int contactId, ContactDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        EnsureNotClosed();

        var contact = FindContact(contactId);
        EnsureContactEmailFree(details.Email, exceptContactId: contactId);
        contact.Update(details);
    }

    // ── User management ─────────────────────────────────────────────────────

    /// <summary>
    /// Links an additional (non-owner) user to this client with the specified permissions.
    /// </summary>
    /// <param name="userId">Identity user ID to link.</param>
    /// <param name="permissions">Granted permissions.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is closed, or the user is the account owner or already linked.</exception>
    public void AddUser(string userId, ClientPermission permissions)
    {
        EnsureNotClosed();

        if (userId == UserId)
        {
            throw new DomainRuleException(
                ClientsErrors.ClientUserIsOwner, "Cannot add the account owner as an additional user.");
        }

        if (_users.Any(u => u.UserId == userId))
        {
            throw new DomainRuleException(
                ClientsErrors.ClientUserLinkDuplicate, $"User {userId} is already linked to this client.");
        }

        _users.Add(ClientUser.Create(Id, userId, permissions));
    }

    /// <summary>
    /// Removes a non-owner user from this client.
    /// </summary>
    /// <param name="userId">Identity user ID to remove.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown when the account is closed, the user is the account owner (the owner changes only by
    /// <see cref="TransferOwnership"/>), or the user is not linked.
    /// </exception>
    public void RemoveUser(string userId)
    {
        EnsureNotClosed();

        if (userId == UserId)
        {
            throw new DomainRuleException(
                ClientsErrors.ClientOwnerCannotBeRemoved, "The account owner cannot be removed; transfer ownership first.");
        }

        _users.Remove(FindUser(userId));
    }

    /// <summary>
    /// Updates the permissions for a non-owner user linked to this client.
    /// </summary>
    /// <param name="userId">Identity user ID whose permissions to update.</param>
    /// <param name="permissions">New permissions value.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is closed or the user is not linked.</exception>
    public void UpdateUserPermissions(string userId, ClientPermission permissions)
    {
        EnsureNotClosed();
        FindUser(userId).UpdatePermissions(permissions);
    }

    /// <summary>
    /// Transfers account ownership to a different user.
    /// If the new owner was a non-owner user, they leave the additional users list. The previous
    /// owner stays on the account as an additional user with every permission, so a transfer never
    /// locks the person who held the account out of it; removing them afterwards is a separate,
    /// deliberate step.
    /// </summary>
    /// <param name="newOwnerUserId">Identity user ID of the new owner.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is closed or the new owner is already the owner.</exception>
    public void TransferOwnership(string newOwnerUserId)
    {
        EnsureNotClosed();

        if (newOwnerUserId == UserId)
        {
            throw new DomainRuleException(
                ClientsErrors.ClientTransferSameOwner, "This user is already the account owner.");
        }

        var existing = _users.FirstOrDefault(u => u.UserId == newOwnerUserId);
        if (existing is not null)
        {
            _users.Remove(existing);
        }

        var previousOwnerUserId = UserId;
        UserId = newOwnerUserId;
        _users.Add(ClientUser.Create(Id, previousOwnerUserId, ClientPermission.All));
    }

    /// <summary>
    /// Adds an amount to the client's credit balance.
    /// </summary>
    /// <param name="amount">The positive amount to add.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not greater than 0.</exception>
    public void AddCredit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be greater than 0.");
        }

        CreditBalance += amount;
    }

    /// <summary>
    /// Deducts an amount from the client's credit balance.
    /// </summary>
    /// <param name="amount">The positive amount to deduct.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not greater than 0.</exception>
    /// <exception cref="DomainRuleException">Thrown when the amount exceeds the current credit balance.</exception>
    public void DeductCredit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Deduction amount must be greater than 0.");
        }

        if (amount > CreditBalance)
        {
            throw new DomainRuleException(
                ClientsErrors.ClientCreditInsufficient,
                $"Cannot deduct {amount} — current credit balance is {CreditBalance}.");
        }

        CreditBalance -= amount;
    }

    /// <summary>
    /// Suspends the account and raises <see cref="ClientSuspendedEvent"/>. Allowed from Active and
    /// Inactive; a suspended or closed account refuses.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is already suspended or closed.</exception>
    public void Suspend(DateTimeOffset now)
    {
        if (Status is ClientStatus.Suspended or ClientStatus.Closed)
        {
            throw new DomainRuleException(
                ClientsErrors.ClientSuspendNotAllowed, $"Cannot suspend a client with status {Status}.");
        }

        Status = ClientStatus.Suspended;
        AddDomainEvent(() => new ClientSuspendedEvent(Id, now));
    }

    /// <summary>
    /// Permanently closes the account and raises <see cref="ClientClosedEvent"/>. Allowed from every
    /// status but Closed; there is no way back.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is already closed.</exception>
    public void Close(DateTimeOffset now)
    {
        if (Status == ClientStatus.Closed)
        {
            throw new DomainRuleException(ClientsErrors.ClientCloseNotAllowed, "Account is already closed.");
        }

        Status = ClientStatus.Closed;
        AddDomainEvent(() => new ClientClosedEvent(Id, UserId, now));
    }

    /// <summary>
    /// Reactivates a suspended or inactive account and raises <see cref="ClientActivatedEvent"/>.
    /// No-ops (and raises nothing) if the account is already active.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is closed.</exception>
    public void Activate(DateTimeOffset now)
    {
        if (Status == ClientStatus.Closed)
        {
            throw new DomainRuleException(ClientsErrors.ClientActivateNotAllowed, "Cannot activate a closed account.");
        }

        if (Status == ClientStatus.Active)
        {
            return;
        }

        Status = ClientStatus.Active;
        AddDomainEvent(() => new ClientActivatedEvent(Id, now));
    }

    /// <summary>
    /// Marks an active account inactive and raises <see cref="ClientDeactivatedEvent"/>: it stays on
    /// file and can be activated again, but is no longer treated as a current customer. No-ops (and
    /// raises nothing) if the account is already inactive. A suspended account is not deactivated
    /// directly: it is activated first, so lifting a suspension is always an explicit step.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <exception cref="DomainRuleException">Thrown when the account is suspended or closed.</exception>
    public void Deactivate(DateTimeOffset now)
    {
        if (Status is ClientStatus.Suspended or ClientStatus.Closed)
        {
            throw new DomainRuleException(
                ClientsErrors.ClientDeactivateNotAllowed, $"Cannot deactivate a client with status {Status}.");
        }

        if (Status == ClientStatus.Inactive)
        {
            return;
        }

        Status = ClientStatus.Inactive;
        AddDomainEvent(() => new ClientDeactivatedEvent(Id, now));
    }

    /// <summary>
    /// Writes the account holder's name and company, the one place their shape is checked so
    /// creating and updating a client cannot disagree. The last name may be empty: a person with a
    /// single name is a real customer.
    /// </summary>
    /// <param name="firstName">The first name; not blank.</param>
    /// <param name="lastName">The last name; may be empty.</param>
    /// <param name="companyName">The optional company name.</param>
    /// <exception cref="ArgumentException">Thrown when the first name is blank or a value is longer than its column.</exception>
    private void SetName(string firstName, string lastName, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > NameMaxLength)
        {
            throw new ArgumentException($"A client's first name is required and at most {NameMaxLength} characters.", nameof(firstName));
        }

        if (lastName is null || lastName.Length > NameMaxLength)
        {
            throw new ArgumentException($"A client's last name is at most {NameMaxLength} characters.", nameof(lastName));
        }

        if (companyName is { Length: > CompanyNameMaxLength })
        {
            throw new ArgumentException($"A client's company name is at most {CompanyNameMaxLength} characters.", nameof(companyName));
        }

        FirstName = firstName;
        LastName = lastName;
        CompanyName = companyName;
    }

    /// <summary>Adds a contact once the caller has decided whether this account may take one.</summary>
    /// <param name="details">What was entered about the contact.</param>
    /// <returns>The new contact.</returns>
    /// <exception cref="DomainRuleException">Thrown when another contact already uses the email address.</exception>
    private Contact AppendContact(ContactDetails details)
    {
        EnsureContactEmailFree(details.Email, exceptContactId: null);

        var contact = new Contact(Id, details);
        _contacts.Add(contact);
        return contact;
    }

    /// <summary>Refuses a change to the people and contacts of an account that is closed for good.</summary>
    /// <exception cref="DomainRuleException">Thrown when the account is closed.</exception>
    private void EnsureNotClosed()
    {
        if (Status == ClientStatus.Closed)
        {
            throw new DomainRuleException(ClientsErrors.ClientRecordClosed, "The account is closed, so its contacts and users cannot be changed.");
        }
    }

    /// <summary>Refuses an email address another contact of this client already uses.</summary>
    /// <param name="email">The address about to be stored, compared without regard to case or surrounding space.</param>
    /// <param name="exceptContactId">The contact being updated, whose own address is not a clash, or <see langword="null"/> when adding.</param>
    /// <exception cref="DomainRuleException">Thrown when another contact uses the address.</exception>
    private void EnsureContactEmailFree(string email, int? exceptContactId)
    {
        var wanted = email?.Trim();
        var taken = _contacts.Any(c =>
            c.Id != exceptContactId && string.Equals(c.Email, wanted, StringComparison.OrdinalIgnoreCase));
        if (taken)
        {
            throw new DomainRuleException(ClientsErrors.ClientContactEmailInUse, "Another contact of this client already uses this email address.");
        }
    }

    /// <summary>Finds one of this client's contacts.</summary>
    /// <param name="contactId">The contact's primary key.</param>
    /// <returns>The contact.</returns>
    /// <exception cref="DomainRuleException">Thrown when the client has no such contact.</exception>
    private Contact FindContact(int contactId) =>
        _contacts.FirstOrDefault(c => c.Id == contactId)
            ?? throw new DomainRuleException(ClientsErrors.ClientContactNotFound, $"Contact {contactId} not found.");

    /// <summary>Finds one of this client's additional users.</summary>
    /// <param name="userId">The Identity user ID.</param>
    /// <returns>The link.</returns>
    /// <exception cref="DomainRuleException">Thrown when the user is not linked to this client.</exception>
    private ClientUser FindUser(string userId) =>
        _users.FirstOrDefault(u => u.UserId == userId)
            ?? throw new DomainRuleException(
                ClientsErrors.ClientUserNotLinked, $"User {userId} is not linked to this client.");
}
