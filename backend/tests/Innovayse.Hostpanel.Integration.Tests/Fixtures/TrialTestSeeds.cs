using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeds free-trial services the way an order creates them: an identity-backed client (so trial and invoice
/// mail can be read back), a product with the trial rules, and a monthly trial service, every row stamped at
/// <see cref="TestMoments.Seeded"/>.
/// </summary>
internal static class TrialTestSeeds
{
    /// <summary>Seeds a client, a product and a trial service of it.</summary>
    /// <param name="services">The services of the host to seed through.</param>
    /// <param name="seed">The trial rules and the state of the service.</param>
    /// <returns>The client id, the service id and the client's email address.</returns>
    public static async Task<(int ClientId, int ServiceId, string Email)> SeedTrialAsync(IServiceProvider services, TrialSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "trial");

        var group = ProductGroup.Create($"trials-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var trial = new ProductTrial(true, seed.Days, seed.AutoConvert, ProductTrial.ParseReminderDays(seed.ReminderDays), seed.GraceDays);
        var product = Product.Create(group.Id, "Trial Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
        product.SetPrice("USD", BillingCycle.Monthly, seed.Price);
        product.ConfigureTrial(trial);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.CreateOrdered(
            client.Id, product.Id, BillingCycle.Monthly, null, 0m, seed.Price, null, null, TestMoments.Seeded, trial: trial);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        if (seed.Activate)
        {
            service.ActivateDigital(TestMoments.Seeded);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        if (seed.EndsAt is { } endsAt)
        {
            // An activated trial renews at its end, so the renewal date moves with it.
            await context.ClientServices.Where(s => s.Id == service.Id)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(s => s.TrialEndsAt, endsAt)
                        .SetProperty(s => s.NextRenewalAt, s => s.NextRenewalAt == null ? null : (DateTimeOffset?)endsAt),
                    CancellationToken.None);
        }

        return (client.Id, service.Id, email);
    }

    /// <summary>
    /// Builds a host on a clock standing at <see cref="TestMoments.Seeded"/> whose domain events are dispatched and
    /// whose SMTP relay is <paramref name="sender"/>, so a use case's mail can be read back. Seed through
    /// <see cref="SettleAsync{T}"/>, so the seeding's own events are handled before the test acts.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="sender">The fake relay the host's mail goes to.</param>
    /// <returns>The isolated host.</returns>
    public static ClockedHost LiveHost(IntegrationTestFactory factory, FakeEmailSender sender) =>
        new(factory, configure: builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(sender);
        }));

    /// <summary>Runs an action and waits until every message it set off has been handled, the mail its events queue included.</summary>
    /// <typeparam name="T">The action's result type.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="action">The action, usually a seed or one command.</param>
    /// <returns>The action's result.</returns>
    public static async Task<T> SettleAsync<T>(IServiceProvider services, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        T result = default!;
        await services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ => result = await action()));
        return result;
    }

    /// <summary>Begins the conversion of a seeded trial the way the conversion use case does, and saves it.</summary>
    /// <param name="services">The services of the host to write through.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <param name="now">The moment of the conversion.</param>
    /// <returns>The start of the paid period and the due date of its invoice, as stored.</returns>
    public static async Task<(DateTimeOffset Start, DateTimeOffset DueAt)> BeginConversionAsync(
        IServiceProvider services, int serviceId, DateTimeOffset now)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        service.BeginTrialConversion(now);
        await context.SaveChangesAsync(CancellationToken.None);

        return (service.NextRenewalAt!.Value, service.TrialPaymentDueAt!.Value);
    }
}
