using System.ComponentModel.DataAnnotations;

namespace Innovayse.Hostpanel.Architecture.Tests.Planted;

/// <summary>A stand-in for an options class that breaks the rule with a string-bounded <see cref="TimeSpan"/> range.</summary>
public sealed class PlantedLazyRangeType
{
    /// <summary>The forbidden lazily converted range the rule must catch.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:00:10")]
    public TimeSpan Timeout { get; set; }
}
