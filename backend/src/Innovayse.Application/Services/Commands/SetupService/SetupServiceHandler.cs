namespace Innovayse.Application.Services.Commands.SetupService;

using Innovayse.Application.Common;
using Innovayse.Application.Servers;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="SetupServiceCommand"/> by selecting the best server,
/// provisioning the hosting account via the appropriate provider,
/// and activating the service.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="productRepo">Product repository to determine server module type.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
/// <param name="serverSelector">Selects the optimal server using proportional fill strategy.</param>
/// <param name="unitOfWork">Unit of work for persisting changes.</param>
public sealed class SetupServiceHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IProvisioningProviderFactory providerFactory,
    IServerSelector serverSelector,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Sets up the service with the provided domain, username, and password,
    /// selects the best available server, provisions the account, and activates the service.
    /// </summary>
    /// <param name="cmd">The setup command with hosting details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found, not in Pending status, no server is available, or provisioning fails.
    /// </exception>
    public async Task HandleAsync(SetupServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.Status is not ServiceStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot set up a service with status {service.Status}. Only Pending services can be set up.");
        }

        // Determine the server module type from the product (default to CWP7 for now)
        var module = ServerModule.Cwp7;
        var product = await productRepo.FindByIdAsync(service.ProductId, ct);
        if (product is not null)
        {
            module = MapProductTypeToModule(product.Type);
        }

        // Select the best available server using proportional fill
        var server = await serverSelector.SelectAsync(module, ct)
            ?? throw new InvalidOperationException(
                $"No eligible {module} server available for provisioning.");

        // Create a provider for the selected server
        var provider = providerFactory.CreateFor(server);

        // Provision the account
        var request = new ProvisionRequest(
            service.Id,
            cmd.Domain,
            cmd.Username,
            cmd.Password,
            product?.PackageName ?? "default");

        var result = await provider.ProvisionAsync(request, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Provisioning failed for service {cmd.ServiceId}: {result.ErrorMessage}");
        }

        // Save the client-provided hosting details and assign the server
        service.Update(
            domain: cmd.Domain,
            dedicatedIp: service.DedicatedIp,
            username: cmd.Username,
            password: cmd.Password,
            billingCycle: service.BillingCycle,
            recurringAmount: service.RecurringAmount,
            paymentMethod: service.PaymentMethod,
            nextRenewalAt: service.NextRenewalAt,
            subscriptionId: service.SubscriptionId,
            overrideAutoSuspend: service.OverrideAutoSuspend,
            suspendUntil: service.SuspendUntil,
            autoTerminateEndOfCycle: service.AutoTerminateEndOfCycle,
            autoTerminateReason: service.AutoTerminateReason,
            adminNotes: service.AdminNotes,
            provisioningRef: service.ProvisioningRef,
            firstPaymentAmount: service.FirstPaymentAmount,
            promotionCode: service.PromotionCode,
            terminatedAt: service.TerminatedAt,
            serverId: server.Id,
            quantity: service.Quantity,
            productId: null);

        service.Activate(result.ProvisioningRef!);

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Maps a product type to the server module that provisions it.
    /// Currently all hosting products use CWP7.
    /// </summary>
    /// <param name="type">The product type.</param>
    /// <returns>The corresponding server module.</returns>
    private static ServerModule MapProductTypeToModule(Domain.Products.ProductType type) => type switch
    {
        Domain.Products.ProductType.SharedHosting => ServerModule.Cwp7,
        Domain.Products.ProductType.Vps => ServerModule.Cwp7,
        Domain.Products.ProductType.Dedicated => ServerModule.Cwp7,
        _ => ServerModule.Cwp7,
    };
}
