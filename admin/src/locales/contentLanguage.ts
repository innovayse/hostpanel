/**
 * The languages the platform holds content in: the languages the API answers in, an email template
 * is written in, a client prefers and storefront content (slides, FAQs, articles) is translated into.
 *
 * The one list of languages. `MessageLanguage`, the languages the admin panel itself is translated
 * into, is a subset of it, so a new language is one member here and the interface joins it later.
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
