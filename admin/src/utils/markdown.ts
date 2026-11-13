/**
 * A small, safe Markdown reader for text that comes from outside the panel (a plugin's release notes).
 *
 * It turns the text into a typed tree that a component draws with ordinary elements. No HTML is ever
 * produced or kept: a raw tag in the source stays plain text, and a link survives only when it is an
 * absolute https address. The supported subset is what release notes use: headings, paragraphs, bullet
 * and numbered lists (nesting is flattened), block quotes, fenced code, rules, and inline bold, italic,
 * code and links. Anything else is shown as the text it is.
 */

/** The kinds of block the reader produces. */
export const MarkdownBlockKind = {
  /** A heading; drawn as a bold line, never as a page heading, so the page outline stays intact. */
  Heading: 'heading',
  /** A paragraph of inline text. */
  Paragraph: 'paragraph',
  /** A bulleted or numbered list. */
  List: 'list',
  /** A quotation. */
  Quote: 'quote',
  /** A fenced block of code, shown verbatim. */
  Code: 'code',
  /** A horizontal rule. */
  Rule: 'rule',
} as const

/** One of the {@link MarkdownBlockKind} values. */
export type MarkdownBlockKind = (typeof MarkdownBlockKind)[keyof typeof MarkdownBlockKind]

/** The kinds of inline node the reader produces. */
export const MarkdownInlineKind = {
  /** Plain text. */
  Text: 'text',
  /** Bold text. */
  Strong: 'strong',
  /** Italic text. */
  Emphasis: 'emphasis',
  /** A short piece of code. */
  Code: 'code',
  /** A link to an https address. */
  Link: 'link',
} as const

/** One of the {@link MarkdownInlineKind} values. */
export type MarkdownInlineKind = (typeof MarkdownInlineKind)[keyof typeof MarkdownInlineKind]

/** One piece of inline content. */
export interface MarkdownInline {
  /** Position in the parsed text, a stable key for the list. */
  id: number
  /** What the piece is. */
  kind: MarkdownInlineKind
  /** The text shown. */
  text: string
  /** The https address of a link; null for every other kind. */
  href: string | null
}

/** One item of a list. */
export interface MarkdownListItem {
  /** Position in the parsed text, a stable key for the list. */
  id: number
  /** The inline content of the item. */
  inlines: MarkdownInline[]
}

/** One block of the parsed text. */
export interface MarkdownBlock {
  /** Position in the parsed text, a stable key for the list. */
  id: number
  /** What the block is. */
  kind: MarkdownBlockKind
  /** The inline content of a heading, paragraph or quote; empty for the other kinds. */
  inlines: MarkdownInline[]
  /** The items of a list; empty for the other kinds. */
  items: MarkdownListItem[]
  /** Whether a list is numbered. */
  ordered: boolean
  /** The verbatim text of a code block; empty for the other kinds. */
  code: string
}

/** Matches one inline construct: code, bold, italic or a link, in that order of priority. */
const INLINE_PATTERN = /`([^`\n]+)`|\*\*([^*\n]+)\*\*|__([^_\n]+)__|\*([^*\s][^*\n]*)\*|(?<![A-Za-z0-9])_([^_\s][^_\n]*)_(?![A-Za-z0-9])|\[([^\]\n]+)\]\(([^)\s]+)\)/g

/** Matches a heading line. */
const HEADING_PATTERN = /^\s{0,3}#{1,6}\s+(.*?)\s*#*\s*$/

/** Matches a horizontal rule line. */
const RULE_PATTERN = /^\s{0,3}([-*_])(\s*\1){2,}\s*$/

/** Matches a list item line; group 1 is the marker, group 2 the text. */
const LIST_PATTERN = /^\s*([-*+]|\d+[.)])\s+(.*)$/

/** Matches the start or end of a fenced code block. */
const FENCE_PATTERN = /^\s{0,3}(```|~~~)/

/** Matches a quote line; group 1 is the text after the marker. */
const QUOTE_PATTERN = /^\s{0,3}>\s?(.*)$/

/**
 * Normalises an address to link to: an absolute https address with a host, written with the
 * scheme and two slashes. A form such as https:host is refused even though the URL parser
 * would read it as an https address, and the address handed back is the parsed one, so what is
 * rendered is exactly what was checked.
 *
 * @param href - The address from the text.
 * @returns The normalised address, or null when it may not become a link.
 */
export const safeLinkHref = (href: string): string | null => {
  if (!href.toLowerCase().startsWith('https://')) {
    return null
  }
  try {
    const url = new URL(href)
    return url.protocol === 'https:' && url.hostname !== '' ? url.href : null
  } catch {
    return null
  }
}

/**
 * Reads the inline constructs of one line of text.
 *
 * @param source - The text of a heading, paragraph, quote line or list item.
 * @param nextId - Gives the next unused id.
 * @returns The inline pieces in order; a link with an unsafe address becomes plain text.
 */
const parseInlines = (source: string, nextId: () => number): MarkdownInline[] => {
  const result: MarkdownInline[] = []
  let last = 0
  /**
   * Adds one piece to the result; empty text adds nothing.
   *
   * @param kind - What the piece is.
   * @param text - The text shown.
   * @param href - The https address of a link, null otherwise.
   */
  const push = (kind: MarkdownInlineKind, text: string, href: string | null = null): void => {
    if (text !== '') {
      result.push({ id: nextId(), kind, text, href })
    }
  }
  for (const match of source.matchAll(INLINE_PATTERN)) {
    push(MarkdownInlineKind.Text, source.slice(last, match.index))
    const [whole, code, strong, strongUnderscore, emphasis, emphasisUnderscore, label, href] = match
    if (code !== undefined) {
      push(MarkdownInlineKind.Code, code)
    } else if (strong !== undefined || strongUnderscore !== undefined) {
      push(MarkdownInlineKind.Strong, strong ?? strongUnderscore ?? '')
    } else if (emphasis !== undefined || emphasisUnderscore !== undefined) {
      push(MarkdownInlineKind.Emphasis, emphasis ?? emphasisUnderscore ?? '')
    } else if (label !== undefined && href !== undefined) {
      const safeHref = safeLinkHref(href)
      if (safeHref === null) {
        // An unsafe or malformed link is shown exactly as written, never as a link.
        push(MarkdownInlineKind.Text, whole)
      } else {
        push(MarkdownInlineKind.Link, label, safeHref)
      }
    } else {
      // An unsafe or malformed link is shown exactly as written, never as a link.
      push(MarkdownInlineKind.Text, whole)
    }
    last = (match.index ?? 0) + whole.length
  }
  push(MarkdownInlineKind.Text, source.slice(last))
  return result
}

/**
 * Tells whether a line starts a block other than a paragraph, so a paragraph ends before it.
 *
 * @param line - The line to test.
 * @returns True for a heading, rule, list item, quote or fence.
 */
const startsBlock = (line: string): boolean =>
  HEADING_PATTERN.test(line) || RULE_PATTERN.test(line) || LIST_PATTERN.test(line) || QUOTE_PATTERN.test(line) || FENCE_PATTERN.test(line)

/**
 * Reads Markdown text into blocks.
 *
 * @param source - The text; null or blank gives no blocks.
 * @returns The blocks in order, with no HTML anywhere in them.
 */
export const parseMarkdown = (source: string | null): MarkdownBlock[] => {
  if (source === null || source.trim() === '') {
    return []
  }
  let counter = 0
  /**
   * Gives the next unused id.
   *
   * @returns A number no node has used yet.
   */
  const nextId = (): number => counter++
  const lines = source.replace(/\r\n?/g, '\n').split('\n')
  const blocks: MarkdownBlock[] = []
  /**
   * Builds a block with every member set.
   *
   * @param kind - What the block is.
   * @param parts - The members that differ from an empty block.
   * @returns The block.
   */
  const block = (kind: MarkdownBlockKind, parts: Partial<MarkdownBlock> = {}): MarkdownBlock => ({
    id: nextId(),
    kind,
    inlines: [],
    items: [],
    ordered: false,
    code: '',
    ...parts,
  })
  let index = 0
  while (index < lines.length) {
    const line = lines[index] ?? ''
    if (line.trim() === '') {
      index++
    } else if (FENCE_PATTERN.test(line)) {
      const fence = FENCE_PATTERN.exec(line)?.[1] ?? '```'
      const body: string[] = []
      index++
      while (index < lines.length && !(lines[index] ?? '').trimStart().startsWith(fence)) {
        body.push(lines[index] ?? '')
        index++
      }
      // Step past the closing fence; an unclosed fence runs to the end of the text.
      index++
      blocks.push(block(MarkdownBlockKind.Code, { code: body.join('\n') }))
    } else if (HEADING_PATTERN.test(line)) {
      const text = HEADING_PATTERN.exec(line)?.[1] ?? ''
      blocks.push(block(MarkdownBlockKind.Heading, { inlines: parseInlines(text, nextId) }))
      index++
    } else if (RULE_PATTERN.test(line)) {
      blocks.push(block(MarkdownBlockKind.Rule))
      index++
    } else if (QUOTE_PATTERN.test(line)) {
      const parts: string[] = []
      while (index < lines.length && QUOTE_PATTERN.test(lines[index] ?? '')) {
        parts.push(QUOTE_PATTERN.exec(lines[index] ?? '')?.[1] ?? '')
        index++
      }
      blocks.push(block(MarkdownBlockKind.Quote, { inlines: parseInlines(parts.join(' ').trim(), nextId) }))
    } else if (LIST_PATTERN.test(line)) {
      const ordered = /^\s*\d/.test(line)
      const items: MarkdownListItem[] = []
      while (index < lines.length && LIST_PATTERN.test(lines[index] ?? '') && !RULE_PATTERN.test(lines[index] ?? '')) {
        const text = LIST_PATTERN.exec(lines[index] ?? '')?.[2] ?? ''
        items.push({ id: nextId(), inlines: parseInlines(text, nextId) })
        index++
      }
      blocks.push(block(MarkdownBlockKind.List, { items, ordered }))
    } else {
      const parts: string[] = []
      while (index < lines.length && (lines[index] ?? '').trim() !== '' && (parts.length === 0 || !startsBlock(lines[index] ?? ''))) {
        parts.push((lines[index] ?? '').trim())
        index++
      }
      blocks.push(block(MarkdownBlockKind.Paragraph, { inlines: parseInlines(parts.join(' '), nextId) }))
    }
  }
  return blocks
}
