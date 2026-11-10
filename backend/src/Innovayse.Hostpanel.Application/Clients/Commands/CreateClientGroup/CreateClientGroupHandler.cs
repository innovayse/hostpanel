using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Clients.Commands.CreateClientGroup;

/// <summary>Creates a client group.</summary>
public sealed class CreateClientGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CreateClientGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group's fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the new group's id. This use case answers no refusal code.</returns>
    public async Task<Result<int>> HandleAsync(CreateClientGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = ClientGroup.Create(cmd.Name, cmd.Description, cmd.DiscountPercent, cmd.Color);

        _context.ClientGroups.Add(group);
        await _context.SaveChangesAsync(ct);

        return Result.Success(group.Id);
    }
}
