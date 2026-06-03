namespace Innovayse.Application.Common;

using System.Text.RegularExpressions;

/// <summary>
/// Lightweight user-agent string parser that extracts device type, OS, and browser.
/// No external dependencies — uses simple regex matching on known patterns.
/// </summary>
public static partial class UserAgentParser
{
    /// <summary>Parsed result from a user-agent string.</summary>
    /// <param name="DeviceType">Desktop, Mobile, or Tablet.</param>
    /// <param name="OperatingSystem">OS name and version (e.g. "Windows 11", "iOS 17.5").</param>
    /// <param name="Browser">Browser name and version (e.g. "Chrome 148.0", "Safari 17.5").</param>
    public record ParsedUserAgent(string DeviceType, string OperatingSystem, string Browser);

    /// <summary>
    /// Parses a user-agent string into device type, OS, and browser.
    /// </summary>
    /// <param name="userAgent">Raw user-agent string.</param>
    /// <returns>Parsed result, or null if the input is null or empty.</returns>
    public static ParsedUserAgent? Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var deviceType = ParseDeviceType(userAgent);
        var os = ParseOperatingSystem(userAgent);
        var browser = ParseBrowser(userAgent);

        return new ParsedUserAgent(deviceType, os, browser);
    }

    /// <summary>Determines whether the device is Mobile, Tablet, or Desktop.</summary>
    /// <param name="ua">Raw user-agent string.</param>
    /// <returns>Device type string.</returns>
    private static string ParseDeviceType(string ua)
    {
        if (TabletRegex().IsMatch(ua))
        {
            return "Tablet";
        }

        if (MobileRegex().IsMatch(ua))
        {
            return "Mobile";
        }

        return "Desktop";
    }

    /// <summary>Extracts the operating system name and version from the user-agent.</summary>
    /// <param name="ua">Raw user-agent string.</param>
    /// <returns>OS description string.</returns>
    private static string ParseOperatingSystem(string ua)
    {
        // iOS
        var match = IosRegex().Match(ua);
        if (match.Success)
        {
            var version = match.Groups[1].Value.Replace('_', '.');
            return $"iOS {version}";
        }

        // Android
        match = AndroidRegex().Match(ua);
        if (match.Success)
        {
            return $"Android {match.Groups[1].Value}";
        }

        // Windows
        match = WindowsNtRegex().Match(ua);
        if (match.Success)
        {
            var ntVersion = match.Groups[1].Value;
            var windowsName = ntVersion switch
            {
                "10.0" => "Windows 10/11",
                "6.3" => "Windows 8.1",
                "6.2" => "Windows 8",
                "6.1" => "Windows 7",
                "6.0" => "Windows Vista",
                _ => $"Windows NT {ntVersion}",
            };
            return windowsName;
        }

        // macOS
        match = MacOsRegex().Match(ua);
        if (match.Success)
        {
            var version = match.Groups[1].Value.Replace('_', '.');
            return $"macOS {version}";
        }

        // Chrome OS
        if (ua.Contains("CrOS", StringComparison.OrdinalIgnoreCase))
        {
            return "Chrome OS";
        }

        // Linux
        if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase))
        {
            return ua.Contains("x86_64", StringComparison.Ordinal) ? "Linux x86_64" : "Linux";
        }

        return "Unknown OS";
    }

    /// <summary>Extracts the browser name and version from the user-agent.</summary>
    /// <param name="ua">Raw user-agent string.</param>
    /// <returns>Browser description string.</returns>
    private static string ParseBrowser(string ua)
    {
        // Edge (must check before Chrome since Edge UA contains "Chrome")
        var match = EdgeRegex().Match(ua);
        if (match.Success)
        {
            return $"Edge {match.Groups[1].Value}";
        }

        // Opera / OPR
        match = OperaRegex().Match(ua);
        if (match.Success)
        {
            return $"Opera {match.Groups[1].Value}";
        }

        // Samsung Browser
        match = SamsungBrowserRegex().Match(ua);
        if (match.Success)
        {
            return $"Samsung Browser {match.Groups[1].Value}";
        }

        // Firefox
        match = FirefoxRegex().Match(ua);
        if (match.Success)
        {
            return $"Firefox {match.Groups[1].Value}";
        }

        // Chrome (check after Edge/Opera/Samsung since they include "Chrome")
        match = ChromeRegex().Match(ua);
        if (match.Success)
        {
            return $"Chrome {match.Groups[1].Value}";
        }

        // Safari (check last since many browsers include "Safari")
        match = SafariVersionRegex().Match(ua);
        if (match.Success)
        {
            return $"Safari {match.Groups[1].Value}";
        }

        return "Unknown Browser";
    }

    /// <summary>Regex for detecting tablet devices.</summary>
    [GeneratedRegex(@"iPad|Android(?!.*Mobile)|Tablet", RegexOptions.IgnoreCase)]
    private static partial Regex TabletRegex();

    /// <summary>Regex for detecting mobile devices.</summary>
    [GeneratedRegex(@"Mobile|iPhone|iPod|Android.*Mobile|Windows Phone|BlackBerry|Opera Mini|IEMobile", RegexOptions.IgnoreCase)]
    private static partial Regex MobileRegex();

    /// <summary>Regex for extracting iOS version.</summary>
    [GeneratedRegex(@"CPU (?:iPhone )?OS (\d+[_\.]\d+(?:[_\.]\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex IosRegex();

    /// <summary>Regex for extracting Android version.</summary>
    [GeneratedRegex(@"Android (\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex AndroidRegex();

    /// <summary>Regex for extracting Windows NT version.</summary>
    [GeneratedRegex(@"Windows NT (\d+\.\d+)")]
    private static partial Regex WindowsNtRegex();

    /// <summary>Regex for extracting macOS version.</summary>
    [GeneratedRegex(@"Mac OS X (\d+[_\.]\d+(?:[_\.]\d+)?)")]
    private static partial Regex MacOsRegex();

    /// <summary>Regex for extracting Edge version.</summary>
    [GeneratedRegex(@"Edg(?:e|A|iOS)?/(\d+[\.\d]*)")]
    private static partial Regex EdgeRegex();

    /// <summary>Regex for extracting Opera version.</summary>
    [GeneratedRegex(@"(?:OPR|Opera)/(\d+[\.\d]*)")]
    private static partial Regex OperaRegex();

    /// <summary>Regex for extracting Samsung Browser version.</summary>
    [GeneratedRegex(@"SamsungBrowser/(\d+[\.\d]*)")]
    private static partial Regex SamsungBrowserRegex();

    /// <summary>Regex for extracting Firefox version.</summary>
    [GeneratedRegex(@"Firefox/(\d+[\.\d]*)")]
    private static partial Regex FirefoxRegex();

    /// <summary>Regex for extracting Chrome version.</summary>
    [GeneratedRegex(@"Chrome/(\d+[\.\d]*)")]
    private static partial Regex ChromeRegex();

    /// <summary>Regex for extracting Safari version.</summary>
    [GeneratedRegex(@"Version/(\d+[\.\d]*).*Safari")]
    private static partial Regex SafariVersionRegex();
}
