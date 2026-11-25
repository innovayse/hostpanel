using System.Diagnostics.CodeAnalysis;

namespace Innovayse.Hostpanel.Shared.Models;

/// <summary>
/// Generic result pattern with typed value.
/// Used for operations that return data on success.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Result<T>.Success/Failure factories are the use-case contract every handler returns.")]
[SuppressMessage(
    "StyleCop.CSharp.DocumentationRules",
    "SA1649:File name should match first type name",
    Justification = "The generic twin lives in ResultOfT.cs beside Result.cs.")]
public class Result<T> : Result
{
    /// <summary>Initializes a new typed result.</summary>
    /// <param name="isSuccess">Whether operation succeeded.</param>
    /// <param name="value">Result value if successful.</param>
    /// <param name="error">Error message if failed.</param>
    /// <param name="errorCode">Error code for client handling.</param>
    /// <param name="errors">Optional field-level errors for form binding.</param>
    private Result(bool isSuccess, T? value, string? error, string? errorCode = null,
        Dictionary<string, List<string>>? errors = null)
        : base(isSuccess, error, errorCode, errors)
    {
        Value = value;
    }

    /// <summary>Result value if successful.</summary>
    public T? Value { get; }

    /// <summary>Create successful result with value.</summary>
    /// <param name="value">Result value.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Create a refusal carrying only its code. The sentence a person reads is resolved from
    /// <c>ErrorMessages.resx</c> by that code at the API boundary, so a handler has none to supply.
    /// </summary>
    /// <remarks>
    /// A separate name rather than a one-argument <c>Failure</c> overload: <c>Failure(string)</c>
    /// already binds its single argument to the <b>message</b>, leaving the code null, and that call
    /// fails silently; the boundary answers <c>HOSTPANEL_UNEXPECTED</c> with the code sitting in the
    /// message slot.
    /// </remarks>
    /// <param name="errorCode">A <c>HostpanelErrorCodes</c> constant.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static new Result<T> FailureCode(string errorCode) => new(false, default, null, errorCode);

    /// <summary>Create failure result with error.</summary>
    /// <param name="error">Human-readable error message.</param>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static new Result<T> Failure(string error, string? errorCode = null) =>
        new(false, default, error, errorCode);

    /// <summary>
    /// Re-types a refusal for a caller that passes another use case's or port's failure on: its code, sentence and
    /// structured details (<c>Errors</c>) all travel, where <see cref="FailureCode"/> would keep the code alone.
    /// </summary>
    /// <param name="failed">The failed outcome to pass on.</param>
    /// <returns>A failed <see cref="Result{T}"/> equal to <paramref name="failed"/> in everything but its value type.</returns>
    public static Result<T> FailureFrom(Result failed)
    {
        ArgumentNullException.ThrowIfNull(failed);

        return new(false, default, failed.Error, failed.ErrorCode, failed.Errors);
    }

    /// <summary>Create failure result bound to a specific form field.</summary>
    /// <param name="error">Human-readable error message.</param>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <param name="fieldName">Form field name this error is bound to (PascalCase).</param>
    /// <returns>A failed <see cref="Result{T}"/> with field-level errors.</returns>
    public static new Result<T> Failure(string error, string errorCode, string fieldName) =>
        new(false, default, error, errorCode,
            new Dictionary<string, List<string>> { { fieldName, [error] } });

    /// <summary>
    /// Create failure result carrying one structured detail the caller can act on, alongside the sentence.
    /// </summary>
    /// <param name="error">Human-readable error message.</param>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <param name="detailKey">The camelCase key the detail is read under, e.g. <c>attachmentId</c>.</param>
    /// <param name="detailValue">The detail itself: an identifier, never a sentence.</param>
    /// <returns>A failed <see cref="Result{T}"/> whose <c>Errors</c> holds the one detail.</returns>
    public static new Result<T> FailureWithDetail(
        string error, string errorCode, string detailKey, string detailValue) =>
        new(false, default, error, errorCode,
            new Dictionary<string, List<string>> { { detailKey, [detailValue] } });
}
