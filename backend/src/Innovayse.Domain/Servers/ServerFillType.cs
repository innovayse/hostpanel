namespace Innovayse.Domain.Servers;

/// <summary>
/// Determines how new accounts are distributed across servers within a group.
/// </summary>
public enum ServerFillType
{
    /// <summary>Always assign to the server with the fewest accounts.</summary>
    LeastFull,

    /// <summary>Fill one server to capacity before moving to the next.</summary>
    FillUntilFull,
}
