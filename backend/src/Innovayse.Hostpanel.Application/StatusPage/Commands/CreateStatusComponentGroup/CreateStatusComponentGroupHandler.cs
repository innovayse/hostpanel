using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusComponentGroup;

/// <summary>Creates a <see cref="StatusComponentGroup"/> and persists it.</summary>
public sealed class CreateStatusComponentGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CreateStatusComponentGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new group's id. This use case refuses nothing the validator lets through.</returns>
    public async Task<Result<int>> HandleAsync(CreateStatusComponentGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = StatusComponentGroup.Create(cmd.Name, cmd.DisplayOrder);
        _context.StatusComponentGroups.Add(group);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(group.Id);
    }
}
