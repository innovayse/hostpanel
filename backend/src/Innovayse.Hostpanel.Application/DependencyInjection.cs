using FluentValidation;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Commands.AcceptInvitation;
using Innovayse.Hostpanel.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Application;

/// <summary>
/// Registers what the Application layer owns: every <see cref="AbstractValidator{T}"/> the
/// commands and queries in this assembly declare.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Adds the Application layer.</summary>
    /// <param name="services">The container.</param>
    /// <returns>The container, for chaining.</returns>
    /// <remarks>
    /// Every <c>AbstractValidator&lt;T&gt;</c> in this assembly, which is where all of them
    /// live: there is not one in the API, the Domain or the Infrastructure project. Registered
    /// Scoped, which is this call's default and is load-bearing rather than incidental:
    /// <c>PlaceOrderValidator</c> takes <c>ICurrentUserService</c>, itself Scoped, so a Singleton
    /// registration here would be a captive dependency that answers every checkout with the first
    /// caller's identity.
    /// <para>
    /// <c>AddValidatorsFromAssemblyContaining</c>, not a hand-written list: a validator that is
    /// added and not registered is exactly the dead file this whole change exists to stop
    /// producing.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AcceptInvitationCommand>();

        // Runs the passes of the recurring jobs under their lease; scoped because it holds the scoped lease port.
        services.AddScoped<RecurringJobs>();

        // One pass of the fulfilment dispatch, run by the scheduled chain and by the pass kicked after a payment.
        services.AddScoped<FulfilmentDispatcher>();

        return services;
    }
}
