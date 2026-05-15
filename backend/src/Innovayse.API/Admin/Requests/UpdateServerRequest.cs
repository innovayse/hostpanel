namespace Innovayse.API.Admin.Requests;
using Innovayse.Domain.Servers;

/// <summary>
/// Request body for updating an existing provisioning server.
/// Identical fields to <see cref="CreateServerRequest"/> — kept separate for future divergence.
/// </summary>
public sealed class UpdateServerRequest : CreateServerRequest { }
