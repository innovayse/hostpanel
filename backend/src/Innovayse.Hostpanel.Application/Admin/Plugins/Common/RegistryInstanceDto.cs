using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Where this installation stands with the plugin registry, as the admin panel shows it. It carries nothing secret:
/// the private key never leaves the settings row it is stored in.
/// </summary>
/// <param name="State">The registration state.</param>
/// <param name="InstanceId">The id the registry gave this installation; <see langword="null"/> before the first registration.</param>
/// <param name="BaseUrl">The public address the installation told the registry; <see langword="null"/> before the first attempt.</param>
/// <param name="ErrorCode">The code of the last failure; <see langword="null"/> when there is none.</param>
/// <param name="Message">The finished sentence for <paramref name="ErrorCode"/> in the caller's language; <see langword="null"/> when there is none.</param>
/// <param name="VerifiedAt">When the registry confirmed the installation; <see langword="null"/> until it did.</param>
/// <param name="NextAttemptAt">When the next automatic attempt is due; <see langword="null"/> when none is waiting.</param>
public sealed record RegistryInstanceDto(
    RegistryInstanceState State,
    Guid? InstanceId,
    string? BaseUrl,
    string? ErrorCode,
    string? Message,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? NextAttemptAt);
