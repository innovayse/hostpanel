using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Faqs;

/// <summary>
/// A locale-specific translation for a <see cref="Faq"/>.
/// Stored in the <c>FaqTranslations</c> table.
/// </summary>
public sealed class FaqTranslation : BaseEntity
{
    /// <summary>Longest a locale code may be: matches the <c>FaqTranslations.Locale</c> column.</summary>
    public const int LocaleMaxLength = 10;

    /// <summary>Longest Question may be: matches the <c>FaqTranslations.Question</c> column.</summary>
    public const int QuestionMaxLength = 500;

    /// <summary>Longest Answer may be: matches the <c>FaqTranslations.Answer</c> column.</summary>
    public const int AnswerMaxLength = 4000;

    /// <summary>Gets the FK to the parent <see cref="Faq"/>.</summary>
    public int FaqId { get; private set; }

    /// <summary>Gets the BCP-47 locale code (e.g. "en", "ru", "hy", "de", "es").</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Gets the question in this locale.</summary>
    public string Question { get; private set; } = string.Empty;

    /// <summary>Gets the answer in this locale.</summary>
    public string Answer { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private FaqTranslation() { }

    /// <summary>
    /// Creates a new question translation.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="question">The question in this locale.</param>
    /// <param name="answer">The answer in this locale.</param>
    /// <returns>A new <see cref="FaqTranslation"/>.</returns>
    internal static FaqTranslation Create(string locale, string question, string answer)
    {
        return new FaqTranslation
        {
            Locale = locale,
            Question = question,
            Answer = answer
        };
    }

    /// <summary>
    /// Updates every translatable field.
    /// </summary>
    /// <param name="question">The question in this locale.</param>
    /// <param name="answer">The answer in this locale.</param>
    internal void Update(string question, string answer)
    {
        Question = question;
        Answer = answer;
    }
}
