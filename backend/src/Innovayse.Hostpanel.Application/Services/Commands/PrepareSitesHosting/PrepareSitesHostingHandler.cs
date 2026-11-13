using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.PrepareSitesHosting;

/// <summary>Handles <see cref="PrepareSitesHostingCommand"/> through the server's SSH key agent (op <c>prepare-site</c>).</summary>
public sealed class PrepareSitesHostingHandler
{
    /// <summary>Reads the service and its product.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Asks the Products module for the product's metadata.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Resolves the service's server, agent and hosting account.</summary>
    private readonly ISshServiceContextResolver _resolver;

    /// <summary>Records the outcome for staff.</summary>
    private readonly ILogger<PrepareSitesHostingHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Reads the service and its product.</param>
    /// <param name="bus">Asks the Products module for the product's metadata.</param>
    /// <param name="resolver">Resolves the service's server, agent and hosting account.</param>
    /// <param name="logger">Records the outcome for staff.</param>
    public PrepareSitesHostingHandler(
        IHostpanelDbContext context, IMessageBus bus, ISshServiceContextResolver resolver, ILogger<PrepareSitesHostingHandler> logger)
    {
        _context = context;
        _bus = bus;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>Prepares the account when the product asks for it.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success (also when nothing had to be done), or why the account could not be prepared.</returns>
    public async Task<Result> HandleAsync(PrepareSitesHostingCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        // The product belongs to the Products module, so its metadata is asked through its query.
        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(new GetProductsByIdsQuery([service.ProductId]), ct);
        var metadata = products.Value?.FirstOrDefault()?.Metadata;
        if (!AsksForSitesPreparation(metadata))
        {
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(service.Domain))
        {
            return Result.FailureCode(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        }

        var context = await _resolver.ResolveAsync(service.Id, ct);
        if (context is not { State: SshAccessState.Available, Provider: { } provider, Account: { } account })
        {
            _logger.LogWarning("Service {ServiceId} asks for Innovayse Sites preparation but its server has no usable SSH agent ({State}).",
                service.Id, context?.State);
            return Result.FailureCode(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        }

        try
        {
            var steps = await provider.PrepareSiteAsync(account, service.Domain, ct);
            _logger.LogInformation("Prepared the hosting account of service {ServiceId} for Innovayse Sites: {Steps}",
                service.Id, string.Join("; ", steps));
            return Result.Success();
        }
        catch (SshProviderException ex)
        {
            _logger.LogError("Preparing the hosting account of service {ServiceId} for Innovayse Sites failed: {Failure}", service.Id, ex.Failure);
            return Result.FailureCode(HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE);
        }
    }

    /// <summary>Whether the product metadata carries <c>{"innovayseSites": {"prepare": true}}</c>.</summary>
    /// <param name="metadata">The product's metadata JSON, if any.</param>
    /// <returns><see langword="true"/> when the product asks for the preparation.</returns>
    public static bool AsksForSitesPreparation(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadata);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("innovayseSites", out var sites)
                && sites.ValueKind == JsonValueKind.Object
                && sites.TryGetProperty("prepare", out var prepare)
                && prepare.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
