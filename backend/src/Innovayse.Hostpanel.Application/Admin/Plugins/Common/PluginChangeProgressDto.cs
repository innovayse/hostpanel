using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One <c>progress</c> event of a streamed plugin change: the stage it is in and, while the package downloads, how much has arrived.</summary>
/// <param name="Stage">The stage the change is in.</param>
/// <param name="BytesDone">The bytes of the package downloaded so far; zero outside the download.</param>
/// <param name="BytesTotal">The size of the package the registry declared; <see langword="null"/> when it declared none or outside the download.</param>
public sealed record PluginChangeProgressDto(PluginChangeStage Stage, long BytesDone = 0, long? BytesTotal = null);
