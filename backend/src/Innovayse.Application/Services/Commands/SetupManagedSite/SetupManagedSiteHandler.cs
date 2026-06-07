namespace Innovayse.Application.Services.Commands.SetupManagedSite;

using System.Security.Cryptography;
using System.Text;
using Innovayse.Application.Common;
using Innovayse.Application.Servers;
using Innovayse.Domain.Products;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="SetupManagedSiteCommand"/> by provisioning a CWP7 hosting account,
/// deploying the TouchEstate theme via SSH, and activating the service.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="productRepo">Product repository.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
/// <param name="serverSelector">Selects the optimal server using proportional fill strategy.</param>
/// <param name="sshExecutor">Executes shell scripts on remote servers via SSH.</param>
/// <param name="unitOfWork">Unit of work for persisting changes.</param>
public sealed class SetupManagedSiteHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IProvisioningProviderFactory providerFactory,
    IServerSelector serverSelector,
    ISshExecutor sshExecutor,
    IUnitOfWork unitOfWork)
{
    /// <summary>Maximum length for the generated CWP7 username prefix.</summary>
    private const int MaxUsernamePrefix = 8;

    /// <summary>Length of the random suffix appended to the username.</summary>
    private const int UsernameSuffixLength = 3;

    /// <summary>Length of the auto-generated password.</summary>
    private const int PasswordLength = 16;

    /// <summary>
    /// Sets up a managed site service: provisions the CWP7 account, deploys the theme via SSH,
    /// stores TouchEstate keys, and activates the service.
    /// </summary>
    /// <param name="cmd">The setup command with domain and TouchEstate keys.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found, not Pending, product is wrong type,
    /// no server is available, provisioning fails, or SSH deployment fails.
    /// </exception>
    public async Task HandleAsync(SetupManagedSiteCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.Status is not ServiceStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot set up a service with status {service.Status}. Only Pending services can be set up.");
        }

        var product = await productRepo.FindByIdAsync(service.ProductId, ct)
            ?? throw new InvalidOperationException($"Product {service.ProductId} not found.");

        if (product.Type is not ProductType.ManagedSiteTouchestate)
        {
            throw new InvalidOperationException(
                $"Product type {product.Type} is not supported by SetupManagedSite. Expected ManagedSiteTouchestate.");
        }

        // Select the best available CWP7 server
        var server = await serverSelector.SelectAsync(ServerModule.Cwp7, ct)
            ?? throw new InvalidOperationException("No eligible CWP7 server available for provisioning.");

        // Auto-generate credentials
        var username = GenerateUsername(cmd.Domain);
        var password = GeneratePassword();

        // Provision the CWP7 account
        var provider = providerFactory.CreateFor(server);
        var request = new ProvisionRequest(
            service.Id,
            cmd.Domain,
            username,
            password,
            "default");

        var result = await provider.ProvisionAsync(request, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Provisioning failed for service {cmd.ServiceId}: {result.ErrorMessage}");
        }

        // Deploy theme via SSH if the product has a deploy repo configured
        if (!string.IsNullOrWhiteSpace(product.DeployRepoUrl))
        {
            await DeployThemeAsync(server, product, username, cmd, ct);
        }

        // Store TouchEstate keys
        service.SetTouchEstateKeys(cmd.TouchEstatePublicKey, cmd.TouchEstateSecretKey);

        // Update service with domain, credentials, and server assignment
        service.Update(
            domain: cmd.Domain,
            dedicatedIp: service.DedicatedIp,
            username: username,
            password: password,
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
    /// Deploys the TouchEstate theme by SSHing into the server, cloning the repo,
    /// generating a <c>.env</c> file with API keys, and running the deploy script.
    /// </summary>
    /// <param name="server">The target CWP7 server.</param>
    /// <param name="product">The product with deploy configuration.</param>
    /// <param name="username">The generated CWP7 account username.</param>
    /// <param name="cmd">The command containing TouchEstate keys.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the SSH deployment fails.</exception>
    private async Task DeployThemeAsync(
        Server server, Product product, string username,
        SetupManagedSiteCommand cmd, CancellationToken ct)
    {
        var branch = string.IsNullOrWhiteSpace(product.DeployBranch) ? "main" : product.DeployBranch;
        var publicHtml = $"/home/{username}/public_html";

        var scriptBuilder = new StringBuilder();
        scriptBuilder.AppendLine($"cd {publicHtml}");
        scriptBuilder.AppendLine($"rm -rf ./*");
        scriptBuilder.AppendLine($"git clone --branch {branch} --depth 1 {product.DeployRepoUrl} .");

        // Generate .env with TouchEstate keys
        scriptBuilder.AppendLine($"cat > {publicHtml}/.env << 'ENVEOF'");
        scriptBuilder.AppendLine($"TOUCHESTATE_PUBLIC_KEY={cmd.TouchEstatePublicKey}");
        scriptBuilder.AppendLine($"TOUCHESTATE_SECRET_KEY={cmd.TouchEstateSecretKey}");
        scriptBuilder.AppendLine("ENVEOF");

        // Run custom deploy script if configured
        if (!string.IsNullOrWhiteSpace(product.DeployScript))
        {
            scriptBuilder.AppendLine(product.DeployScript);
        }

        // Fix file ownership
        scriptBuilder.AppendLine($"chown -R {username}:{username} {publicHtml}");

        var sshResult = await sshExecutor.ExecuteAsync(
            server.IpAddress ?? server.Hostname,
            22,
            "root",
            server.Password,
            server.AccessHash,
            scriptBuilder.ToString(),
            ct);

        if (!sshResult.Success)
        {
            throw new InvalidOperationException(
                $"SSH deployment failed for service {cmd.ServiceId}: {sshResult.Error}");
        }
    }

    /// <summary>
    /// Generates a CWP7-compatible username from the domain name.
    /// Takes up to 8 alphanumeric chars from the domain and appends 3 random digits.
    /// </summary>
    /// <param name="domain">The customer's domain name.</param>
    /// <returns>A username like "exampld123".</returns>
    private static string GenerateUsername(string domain)
    {
        // Strip TLD and non-alphanumeric chars, take up to MaxUsernamePrefix chars
        var parts = domain.Split('.');
        var name = parts[0];
        var clean = new StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) && clean.Length < MaxUsernamePrefix)
            {
                clean.Append(char.ToLowerInvariant(ch));
            }
        }

        if (clean.Length == 0)
        {
            clean.Append("user");
        }

        // Append random digits
        var suffix = RandomNumberGenerator.GetInt32(100, 1000).ToString();
        clean.Append(suffix);

        return clean.ToString();
    }

    /// <summary>
    /// Generates a cryptographically random password of <see cref="PasswordLength"/> characters.
    /// </summary>
    /// <returns>A random alphanumeric password.</returns>
    private static string GeneratePassword()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%";
        var result = new char[PasswordLength];
        for (var i = 0; i < PasswordLength; i++)
        {
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(result);
    }
}
