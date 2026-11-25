using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;

/// <summary>
/// Staff add a service to a client for a hosting account that already exists on a server: restored
/// after a reinstall, moved from another server, or set up by hand. Nothing is created on the panel, and the panel is asked first
/// whether it holds the account.
/// </summary>
/// <param name="ClientId">The client the service belongs to.</param>
/// <param name="ProductId">The product the service is of.</param>
/// <param name="BillingCycle">The billing cycle.</param>
/// <param name="RecurringAmount">The amount each renewal invoice charges.</param>
/// <param name="ServerId">The server the account lives on.</param>
/// <param name="Domain">The account's primary domain.</param>
/// <param name="Username">The account's username on the panel.</param>
/// <param name="StartedAt">When the current billing period began (UTC); <see langword="null"/> means now.</param>
/// <param name="ConfirmUnverified">
/// The admin confirms the account although the server's panel cannot read accounts, so the host cannot check that it exists. It
/// never overrides a panel that answers the account is not there.
/// </param>
/// <param name="ConfirmSharedDomain">The admin confirms the domain although another service that is not terminated already holds it.</param>
public sealed record AddExistingServiceCommand(
    int ClientId,
    int ProductId,
    BillingCycle BillingCycle,
    decimal RecurringAmount,
    int ServerId,
    string Domain,
    string Username,
    DateTimeOffset? StartedAt,
    bool ConfirmUnverified = false,
    bool ConfirmSharedDomain = false);
