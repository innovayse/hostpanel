using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>One line a host logged.</summary>
/// <param name="Level">The level it was logged at.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception logged with the line, or null when there was none.</param>
internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception = null);
