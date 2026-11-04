using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The account a client slot read acts for, once the caller is resolved.</summary>
/// <param name="ClientId">The client account the caller acts for.</param>
/// <param name="Held">The client permissions the caller holds on that account.</param>
public sealed record ClientPluginSlotCaller(int ClientId, ClientPermission Held);
