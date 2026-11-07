/**
 * Helpers that make text from the OpenAPI document safe to put on a VitePress page. Every page is
 * compiled as a Vue template, so a stray `<`, `{{` or quote in a description would break the whole
 * build, not just one sentence.
 */

/** Matches an inline code span, keeping it as the odd entries when used with `String.split`. */
const CODE_SPAN = /(`[^`\n]*`)/

/**
 * Trims every line of a description and collapses the indentation XML doc comments leave behind.
 *
 * @param {string | undefined} text
 * @returns {string}
 */
export const normalise = (text) =>
  (text ?? '')
    .replace(/\r\n?/g, '\n')
    .split('\n')
    .map((line) => line.trim())
    .join('\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim()

/**
 * Escapes the characters a Vue template or Markdown would act on, outside inline code spans.
 *
 * @param {string | undefined} text
 * @returns {string} Markdown prose.
 */
export const prose = (text) =>
  normalise(text)
    .split(CODE_SPAN)
    .map((part, index) =>
      index % 2 === 1
        ? part
        : part
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/\{/g, '&#123;')
          .replace(/\}/g, '&#125;'),
    )
    .join('')
    .split('\n')
    .map((line) => (line.startsWith('#') ? `\\${line}` : line))
    .join('\n')

/**
 * One line of plain text: no Markdown, no line breaks. For titles, table cells and the
 * descriptions of a SettingTable, which are plain text by rule.
 *
 * @param {string | undefined} text
 * @returns {string}
 */
export const plain = (text) => normalise(text).replace(/`/g, '').replace(/\s+/g, ' ').trim()

/**
 * Escapes plain text for a Markdown table cell or a heading.
 *
 * @param {string | undefined} text
 * @returns {string}
 */
export const cell = (text) =>
  plain(text)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\{/g, '&#123;')
    .replace(/\}/g, '&#125;')
    .replace(/\|/g, '&#124;')

/**
 * Serialises a value for a double-quoted Vue attribute such as `:settings="..."`.
 *
 * @param {unknown} value
 * @returns {string}
 */
export const attribute = (value) =>
  JSON.stringify(value)
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')

/**
 * Escapes text for HTML (the sidebar renders its item text as HTML).
 *
 * @param {string} text
 * @returns {string}
 */
export const html = (text) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
