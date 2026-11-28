using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceBillingTerms;

/// <summary>
/// Handles <see cref="UpdateServiceBillingTermsCommand"/>: moves the service to the named product when it
/// changed and replaces its billing terms.
/// </summary>
public sealed class UpdateServiceBillingTermsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to ask the Products module whether a newly named product exists.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to ask the Products module whether a newly named product exists.</param>
    public UpdateServiceBillingTermsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Applies the billing terms to the service.</summary>
    /// <param name="cmd">The new billing terms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service, or <c>PRODUCT_NOT_FOUND</c>
    /// when the service is moved to a product that does not exist, or <c>SERVICE_TRIAL_DUE_DATE_FIXED</c> when its free
    /// trial is running and another next renewal date is given (nothing is saved).
    /// </returns>
    public async Task<Result> HandleAsync(UpdateServiceBillingTermsCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        // Only a change of product is checked: the current product may since have been removed from
        // the catalogue, and that must not block saving the rest of the terms.
        if (cmd.ProductId != service.ProductId)
        {
            if (!await _bus.ProductExistsAsync(cmd.ProductId, ct))
            {
                return Result.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
            }

            service.ChangeProduct(cmd.ProductId);
        }

        try
        {
            service.ChangeBillingTerms(new ServiceBillingTerms(
                cmd.BillingCycle,
                cmd.Quantity,
                cmd.FirstPaymentAmount,
                cmd.RecurringAmount,
                cmd.NextRenewalAt,
                cmd.PaymentMethod,
                cmd.PromotionCode,
                cmd.SubscriptionId));
        }
        catch (DomainRuleException ex)
        {
            // A running trial keeps its due date: its conversion invoice is found by it.
            return Result.FailureCode(ex.Code);
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
