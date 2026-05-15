namespace Innovayse.Domain.Clients;

using Innovayse.Domain.Clients.Events;
using Innovayse.Domain.Common;

/// <summary>
/// Aggregate root for a client account.
/// Linked to an ASP.NET Core Identity user via <see cref="UserId"/>.
/// Contains the billing address inline and a collection of additional contacts.
/// Stored in the <c>clients</c> table.
/// </summary>
public sealed class Client : AggregateRoot
{
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

    /// <summary>Gets whether late fees are applied to this client.</summary>
    public bool LateFees { get; private set; } = true;

    /// <summary>Gets whether overdue notices are sent to this client.</summary>
    public bool OverdueNotices { get; private set; } = true;

    /// <summary>Gets whether this client is tax exempt.</summary>
    public bool TaxExempt { get; private set; }

    /// <summary>Gets whether invoices are generated separately per service.</summary>
    public bool SeparateInvoices { get; private set; }

    /// <summary>Gets whether automatic credit card processing is disabled.</summary>
    public bool DisableCcProcessing { get; private set; }

    /// <summary>Gets whether this client has opted in to marketing emails.</summary>
    public bool MarketingOptIn { get; private set; }

    /// <summary>Gets whether status update emails are sent to this client.</summary>
    public bool StatusUpdate { get; private set; } = true;

    /// <summary>Gets whether single sign-on is allowed for this client.</summary>
    public bool AllowSso { get; private set; } = true;

    /// <summary>Gets the UTC timestamp when the client account was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the additional contacts linked to this client.</summary>
    public IReadOnlyList<Contact> Contacts => _contacts.AsReadOnly();

    /// <summary>Gets the non-owner users linked to this client account.</summary>
    public IReadOnlyList<ClientUser> Users => _users.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Client() : base(0) { }

    /// <summary>
    /// Creates a new active client account and raises <see cref="ClientCreatedEvent"/>.
    /// </summary>
    /// <param name="userId">The Identity user ID to link this client to.</param>
    /// <param name="firstName">The client's first name.</param>
    /// <param name="lastName">The client's last name.</param>
    /// <param name="email">The client's email address (used for the domain event).</param>
    /// <param name="companyName">Optional company name.</param>
    /// <returns>A new, unpersisted <see cref="Client"/> instance.</returns>
    public static Client Create(string userId, string firstName, string lastName, string email, string? companyName = null)
    {
        var client = new Client
        {
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            CompanyName = companyName,
            Status = ClientStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // ClientId is 0 here — EF Core assigns the real Id after SaveChanges.
        // The Unit of Work dispatches domain events post-save, so handlers receive the correct Id.
        client.AddDomainEvent(new ClientCreatedEvent(0, userId, email));

        return client;
    }

    /// <summary>
    /// Updates the client's profile information.
    /// </summary>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    /// <param name="companyName">New company name (null to clear).</param>
    /// <param name="phone">New phone number (null to clear).</param>
    public void Update(string firstName, string lastName, string? companyName, string? phone)
    {
        FirstName = firstName;
        LastName = lastName;
        CompanyName = companyName;
        Phone = phone;
    }

    /// <summary>
    /// Updates the client's billing address.
    /// </summary>
    /// <param name="street">Street line.</param>
    /// <param name="address2">Second address line.</param>
    /// <param name="city">City.</param>
    /// <param name="state">State or region.</param>
    /// <param name="postCode">Postcode or ZIP.</param>
    /// <param name="country">ISO 3166-1 alpha-2 country code.</param>
    public void UpdateAddress(string? street, string? address2, string? city, string? state, string? postCode, string? country)
    {
        Street = street;
        Address2 = address2;
        City = city;
        State = state;
        PostCode = postCode;
        Country = country;
    }

    /// <summary>
    /// Updates the client's billing preferences.
    /// </summary>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="paymentMethod">Preferred payment method label.</param>
    /// <param name="billingContact">Billing contact reference.</param>
    /// <param name="adminNotes">Internal admin notes.</param>
    public void UpdatePreferences(string? currency, string? paymentMethod, string? billingContact, string? adminNotes)
    {
        Currency = currency;
        PaymentMethod = paymentMethod;
        BillingContact = billingContact;
        AdminNotes = adminNotes;
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
    /// <param name="lateFees">Apply late fees.</param>
    /// <param name="overdueNotices">Send overdue notices.</param>
    /// <param name="taxExempt">Tax exempt flag.</param>
    /// <param name="separateInvoices">Separate invoices per service.</param>
    /// <param name="disableCcProcessing">Disable automatic CC processing.</param>
    /// <param name="marketingOptIn">Marketing emails opt-in.</param>
    /// <param name="statusUpdate">Send status update emails.</param>
    /// <param name="allowSso">Allow single sign-on.</param>
    public void UpdateSettings(
        bool lateFees, bool overdueNotices, bool taxExempt, bool separateInvoices,
        bool disableCcProcessing, bool marketingOptIn, bool statusUpdate, bool allowSso)
    {
        LateFees = lateFees;
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
    /// <param name="firstName">The contact's first name.</param>
    /// <param name="lastName">The contact's last name.</param>
    /// <param name="companyName">Optional company name.</param>
    /// <param name="email">The contact's email address.</param>
    /// <param name="phone">Optional phone number.</param>
    /// <param name="type">The contact type.</param>
    /// <param name="street">Optional street address.</param>
    /// <param name="address2">Optional second address line.</param>
    /// <param name="city">Optional city.</param>
    /// <param name="state">Optional state or region.</param>
    /// <param name="postCode">Optional postal code.</param>
    /// <param name="country">Optional ISO 3166-1 alpha-2 country code.</param>
    /// <param name="notifyGeneral">General email notifications.</param>
    /// <param name="notifyInvoice">Invoice email notifications.</param>
    /// <param name="notifySupport">Support email notifications.</param>
    /// <param name="notifyProduct">Product email notifications.</param>
    /// <param name="notifyDomain">Domain email notifications.</param>
    /// <param name="notifyAffiliate">Affiliate email notifications.</param>
    public void AddContact(
        string firstName, string lastName, string? companyName,
        string email, string? phone, ContactType type,
        string? street, string? address2, string? city, string? state, string? postCode, string? country,
        bool notifyGeneral, bool notifyInvoice, bool notifySupport,
        bool notifyProduct, bool notifyDomain, bool notifyAffiliate)
    {
        _contacts.Add(new Contact(
            Id, firstName, lastName, companyName, email, phone, type,
            street, address2, city, state, postCode, country,
            notifyGeneral, notifyInvoice, notifySupport,
            notifyProduct, notifyDomain, notifyAffiliate));
    }

    /// <summary>
    /// Removes a contact by ID. Throws if not found.
    /// </summary>
    /// <param name="contactId">The contact's primary key.</param>
    /// <exception cref="InvalidOperationException">Thrown when the contact is not found.</exception>
    public void RemoveContact(int contactId)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId)
            ?? throw new InvalidOperationException($"Contact {contactId} not found.");
        _contacts.Remove(contact);
    }

    /// <summary>
    /// Updates an existing contact's fields. Throws if the contact is not found.
    /// </summary>
    /// <param name="contactId">The contact's primary key.</param>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    /// <param name="companyName">New company name (null to clear).</param>
    /// <param name="email">New email address.</param>
    /// <param name="phone">New phone number (null to clear).</param>
    /// <param name="type">New contact type.</param>
    /// <param name="street">New street address (null to clear).</param>
    /// <param name="address2">New second address line (null to clear).</param>
    /// <param name="city">New city (null to clear).</param>
    /// <param name="state">New state or region (null to clear).</param>
    /// <param name="postCode">New postal code (null to clear).</param>
    /// <param name="country">New ISO 3166-1 alpha-2 country code (null to clear).</param>
    /// <param name="notifyGeneral">General email notifications.</param>
    /// <param name="notifyInvoice">Invoice email notifications.</param>
    /// <param name="notifySupport">Support email notifications.</param>
    /// <param name="notifyProduct">Product email notifications.</param>
    /// <param name="notifyDomain">Domain email notifications.</param>
    /// <param name="notifyAffiliate">Affiliate email notifications.</param>
    /// <exception cref="InvalidOperationException">Thrown when the contact is not found.</exception>
    public void UpdateContact(
        int contactId, string firstName, string lastName, string? companyName,
        string email, string? phone, ContactType type,
        string? street, string? address2, string? city, string? state, string? postCode, string? country,
        bool notifyGeneral, bool notifyInvoice, bool notifySupport,
        bool notifyProduct, bool notifyDomain, bool notifyAffiliate)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId)
            ?? throw new InvalidOperationException($"Contact {contactId} not found.");
        contact.Update(
            firstName, lastName, companyName, email, phone, type,
            street, address2, city, state, postCode, country,
            notifyGeneral, notifyInvoice, notifySupport,
            notifyProduct, notifyDomain, notifyAffiliate);
    }

    // ── User management ─────────────────────────────────────────────────────

    /// <summary>
    /// Links an additional (non-owner) user to this client with the specified permissions.
    /// </summary>
    /// <param name="userId">Identity user ID to link.</param>
    /// <param name="permissions">Granted permissions.</param>
    /// <exception cref="InvalidOperationException">Thrown when the user is the account owner or already linked.</exception>
    public void AddUser(string userId, ClientPermission permissions)
    {
        if (userId == UserId)
            throw new InvalidOperationException("Cannot add the account owner as an additional user.");
        if (_users.Any(u => u.UserId == userId))
            throw new InvalidOperationException($"User {userId} is already linked to this client.");
        _users.Add(ClientUser.Create(Id, userId, permissions));
    }

    /// <summary>
    /// Removes a non-owner user from this client.
    /// </summary>
    /// <param name="userId">Identity user ID to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the user is not found.</exception>
    public void RemoveUser(string userId)
    {
        var user = _users.FirstOrDefault(u => u.UserId == userId)
            ?? throw new InvalidOperationException($"User {userId} is not linked to this client.");
        _users.Remove(user);
    }

    /// <summary>
    /// Updates the permissions for a non-owner user linked to this client.
    /// </summary>
    /// <param name="userId">Identity user ID whose permissions to update.</param>
    /// <param name="permissions">New permissions value.</param>
    /// <exception cref="InvalidOperationException">Thrown when the user is not found.</exception>
    public void UpdateUserPermissions(string userId, ClientPermission permissions)
    {
        var user = _users.FirstOrDefault(u => u.UserId == userId)
            ?? throw new InvalidOperationException($"User {userId} is not linked to this client.");
        user.UpdatePermissions(permissions);
    }

    /// <summary>
    /// Transfers account ownership to a different user.
    /// If the new owner was a non-owner user, they are removed from the additional users list.
    /// The previous owner is NOT automatically added as a non-owner user.
    /// </summary>
    /// <param name="newOwnerUserId">Identity user ID of the new owner.</param>
    /// <exception cref="InvalidOperationException">Thrown when the new owner is already the owner.</exception>
    public void TransferOwnership(string newOwnerUserId)
    {
        if (newOwnerUserId == UserId)
            throw new InvalidOperationException("This user is already the account owner.");

        // Remove new owner from additional users if they were linked
        var existing = _users.FirstOrDefault(u => u.UserId == newOwnerUserId);
        if (existing is not null)
            _users.Remove(existing);

        UserId = newOwnerUserId;
    }

    /// <summary>Suspends the account. Throws if already suspended or closed.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the account is not in an active state.</exception>
    public void Suspend()
    {
        if (Status is ClientStatus.Suspended or ClientStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot suspend a client with status {Status}.");
        }

        Status = ClientStatus.Suspended;
    }

    /// <summary>Permanently closes the account.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the account is already closed.</exception>
    public void Close()
    {
        if (Status == ClientStatus.Closed)
            throw new InvalidOperationException("Account is already closed.");

        Status = ClientStatus.Closed;
    }

    /// <summary>
    /// Reactivates a suspended or inactive account.
    /// No-ops if the account is already active.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the account is closed.</exception>
    public void Activate()
    {
        if (Status == ClientStatus.Closed)
        {
            throw new InvalidOperationException("Cannot activate a closed account.");
        }

        Status = ClientStatus.Active;
    }
}
