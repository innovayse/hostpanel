namespace Innovayse.Hostpanel.Shared.Models.Interfaces;

/// <summary>
///     A handler result that carries the reply to give, status included, because the status is not the
///     use case's to choose: a plugin decides it. Implemented by the plugin reply DTO so
///     <c>BaseApiController.HandleForwarded</c> can turn a <see cref="Result{T}"/> into the response without the
///     controller naming a status or touching the body.
/// </summary>
public interface IForwardedReply
{
    /// <summary>The HTTP status to answer with: a success status without an <see cref="ErrorCode"/>, a failure status with one.</summary>
    public int Status { get; }

    /// <summary>The value written as the JSON body of a success; <see langword="null"/> for a reply with no content or a problem.</summary>
    public object? Body { get; }

    /// <summary>The error code of a problem, written as the <c>type</c> of the standard error body; <see langword="null"/> for a success.</summary>
    public string? ErrorCode { get; }

    /// <summary>The finished sentence of a problem, written as the <c>detail</c> of the standard error body; <see langword="null"/> for a success.</summary>
    public string? Detail { get; }
}
