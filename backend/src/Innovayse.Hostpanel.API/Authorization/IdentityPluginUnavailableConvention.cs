using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// Declares 503 <see cref="ErrorResponse"/> once for every client portal action: the permission filter and the
/// portal policy answer it when the enabled identity plugin cannot say which account the caller acts for, so
/// it is part of the contract of every action that carries <see cref="RequiresClientPermissionAttribute"/> or
/// <see cref="AvailableToEveryClientUserAttribute"/>, on the action or its controller.
/// </summary>
public sealed class IdentityPluginUnavailableConvention : IActionModelConvention
{
    /// <inheritdoc/>
    public void Apply(ActionModel action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var portal = action.Attributes.Any(IsPortalDeclaration)
            || action.Controller.Attributes.Any(IsPortalDeclaration);
        var declared = action.Filters.OfType<ProducesResponseTypeAttribute>()
            .Any(produces => produces.StatusCode == StatusCodes.Status503ServiceUnavailable);

        if (portal && !declared)
        {
            action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable));
        }
    }

    /// <summary>Whether an attribute is one of the two portal declarations.</summary>
    /// <param name="attribute">An attribute on an action or controller.</param>
    /// <returns><see langword="true"/> for a portal declaration.</returns>
    private static bool IsPortalDeclaration(object attribute) =>
        attribute is RequiresClientPermissionAttribute or AvailableToEveryClientUserAttribute;
}
