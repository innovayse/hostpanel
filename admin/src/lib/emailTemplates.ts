/**
 * Pure helpers of the email templates list: how a template's slug becomes a group and a readable name, how a subject
 * line is cut into text and `{{ placeholder }}` parts, and how a language is written as a badge code.
 */

/** Opening and closing marks of a placeholder in a template's subject or body. */
const PLACEHOLDER_PATTERN = /\{\{\s*([^{}]+?)\s*\}\}/g

/** One piece of a subject line: plain text, or a `{{ placeholder }}`. */
export interface SubjectPart {
  /** The text, or for a placeholder the name inside the braces, trimmed. */
  text: string
  /** Whether the piece is a placeholder. */
  placeholder: boolean
}

/**
 * The group a template belongs to: the first word of its slug (`domain-expired` is in `domain`).
 *
 * @param slug - The template slug, lower-case words joined by hyphens.
 * @returns The group key.
 */
export const emailTemplateGroupOf = (slug: string): string => slug.split('-')[0] ?? slug

/**
 * Writes a slug as words for a person: hyphens become spaces and the first letter is capital. The fallback name of a
 * template that has no translated one, such as a template a plugin adds.
 *
 * @param slug - The template slug or a group key.
 * @returns The readable name, such as "Domain expired".
 */
export const humanizeSlug = (slug: string): string => {
  const words = slug.replace(/-/g, ' ').trim()
  return words.charAt(0).toUpperCase() + words.slice(1)
}

/**
 * Cuts a subject line into plain text and `{{ placeholder }}` parts, in reading order, so a screen can draw the
 * placeholders apart from the words around them.
 *
 * @param subject - The subject line as stored.
 * @returns The parts; empty for an empty subject. Joining the texts of the plain parts and the braces of the
 * placeholders gives the subject back.
 */
export const splitSubject = (subject: string): SubjectPart[] => {
  const parts: SubjectPart[] = []
  let cursor = 0
  for (const match of subject.matchAll(PLACEHOLDER_PATTERN)) {
    const start = match.index ?? 0
    if (start > cursor) {
      parts.push({ text: subject.slice(cursor, start), placeholder: false })
    }
    parts.push({ text: match[1] ?? '', placeholder: true })
    cursor = start + match[0].length
  }
  if (cursor < subject.length) {
    parts.push({ text: subject.slice(cursor), placeholder: false })
  }
  return parts
}

/**
 * Writes a language as the short upper-case code a badge shows.
 *
 * @param language - The language code, such as `en`.
 * @returns The code in capitals, such as `EN`.
 */
export const languageBadge = (language: string): string => language.toUpperCase()
