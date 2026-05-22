namespace Innovayse.Domain.Billing;

/// <summary>Lifecycle stages for a <see cref="Quote"/>.</summary>
public enum QuoteStage
{
    /// <summary>Quote is being prepared and has not been sent to the client.</summary>
    Draft,

    /// <summary>Quote has been delivered to the client for review.</summary>
    Delivered,

    /// <summary>Quote is temporarily paused pending further discussion.</summary>
    OnHold,

    /// <summary>Quote has been accepted by the client.</summary>
    Accepted,

    /// <summary>Quote was not accepted and the opportunity is lost.</summary>
    Lost,

    /// <summary>Quote has been permanently abandoned.</summary>
    Dead,
}
