using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.InitiateOutgoingTransfer;

/// <summary>
/// Initiates an outgoing domain transfer at the registrar, retrieves the EPP code,
/// and stores it on the domain aggregate.
/// </summary>
public sealed class InitiateOutgoingTransferHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<InitiateOutgoingTransferHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public InitiateOutgoingTransferHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<InitiateOutgoingTransferHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="InitiateOutgoingTransferCommand"/>.
    /// </summary>
    /// <param name="cmd">The initiate outgoing transfer command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The EPP (authorization) code, as an <see cref="EppCodeDto"/>, to hand to the gaining registrar, or <c>DOMAIN_NOT_FOUND</c> when
    /// the id names no domain, or <c>DOMAIN_NO_REGISTRAR_REF</c> when the domain carries no registrar
    /// reference, or <c>DOMAIN_OUTGOING_TRANSFER_UPSTREAM_FAILED</c> when the registrar rejects the
    /// request, or <c>DOMAIN_EPP_CODE_UPSTREAM_FAILED</c> when the registrar returns no code.
    /// </returns>
    public async Task<Result<EppCodeDto>> HandleAsync(InitiateOutgoingTransferCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId, ct);
        if (domain is null)
        {
            return Result<EppCodeDto>.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.RegistrarRef is null)
        {
            return Result<EppCodeDto>.FailureCode(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
        }

        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.InitiateOutgoingTransferAsync(new DomainRef(domain.Name, domain.RegistrarRef), ct);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar rejected outgoing transfer initiation for '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result<EppCodeDto>.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DOMAIN_OUTGOING_TRANSFER_UPSTREAM_FAILED));
        }

        var eppCode = await registrar.GetEppCodeAsync(new DomainRef(domain.Name, domain.RegistrarRef), ct);
        if (eppCode is null)
        {
            _logger.LogWarning("Registrar returned no EPP code for domain '{DomainName}'.", domain.Name);
            return Result<EppCodeDto>.FailureCode(HostpanelErrorCodes.DOMAIN_EPP_CODE_UPSTREAM_FAILED);
        }

        domain.SetEppCode(eppCode);
        await _context.SaveChangesAsync(ct);
        return Result<EppCodeDto>.Success(new EppCodeDto(eppCode));
    }
}
