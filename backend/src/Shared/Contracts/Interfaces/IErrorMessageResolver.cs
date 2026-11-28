namespace Innovayse.Hostpanel.Shared.Contracts.Interfaces;

/// <summary>
/// Turns an error code into the sentence a person reads, in the current request's language.
/// </summary>
/// <remarks>
/// A contract declared here and implemented elsewhere, the Application layer implements it over
/// <c>ErrorMessages.resx</c>, so the base controller can localize without referencing the
/// Application layer's resource types.
/// </remarks>
public interface IErrorMessageResolver
{
    /// <summary>
    /// The localized sentence for <paramref name="code"/>, or the English fallback when the code has none.
    /// </summary>
    /// <param name="code">An error code from the registry.</param>
    /// <param name="arguments">
    /// Values for the <c>{0}</c>, <c>{1}</c> placeholders of the sentence, in order; none for a sentence without any.
    /// </param>
    /// <returns>A sentence, never the bare code.</returns>
    public string Resolve(string code, params object[] arguments);
}
