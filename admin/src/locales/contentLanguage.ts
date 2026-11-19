/**
 * The languages the platform holds content in: the languages the API answers in, an email template
 * is written in, a client prefers and storefront content (slides, FAQs, articles) is translated into.
 *
 * Not the same set as `MessageLanguage`, which is the languages the admin panel itself is
 * translated into. The two meet where the panel shows content in the language of its user.
 */

/** A language of the platform's content. */
export enum ContentLanguage {
  /** English; the language every record must have. */
  En = 'en',
  /** Russian. */
  Ru = 'ru',
  /** Armenian. */
  Hy = 'hy',
  /** German. */
  De = 'de',
  /** Spanish. */
  Es = 'es',
}
