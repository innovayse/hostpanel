/**
 * Reader for the small Markdown subset an AI reply is written in: paragraphs, headings, flat lists, fenced code,
 * `**bold**`, `*italic*`, `` `code` `` and backslash escapes.
 *
 * The reply is untrusted text from a plugin and its model, so it never becomes markup: this module builds the same node
 * tree as `richText.ts` and only ever creates the tags in `MARKDOWN_TAG_CLASSES`, without one attribute coming from the
 * text except a digits-only list `start`. HTML in a reply stays visible text, escaped by Vue. A Markdown link and image stay
 * literal text too, because an address in a plugin's view may only render through `pluginLinkTarget`, which a chat's
 * `link` event already goes through.
 *
 * A reply arrives in pieces and is parsed again on each one, so an unfinished construct must stay readable: an unclosed
 * `**` is literal until its closer arrives, and an unclosed fence is code up to the end. Scanning has no regular
 * expression over the inline text and a bounded nesting depth, so a hostile reply cannot make it slow.
 *
 * @module utils/ui/markdownText
 */

import type { RichElementNode, RichNode } from '~/utils/ui/richText'
import { UiRichNodeType } from '~/utils/ui/enums'

/** Classes per output tag, sized for a chat bubble; `richText.ts` sizes its own for a whole article. */
export const MARKDOWN_TAG_CLASSES: Readonly<Record<string, string>> = {
  p: 'mb-2 last:mb-0',
  h4: 'mb-1 mt-2 font-semibold first:mt-0',
  ul: 'mb-2 list-disc space-y-1 pl-5 last:mb-0',
  ol: 'mb-2 list-decimal space-y-1 pl-5 last:mb-0',
  code: 'rounded bg-gray-100 px-1 font-mono text-xs dark:bg-white/10',
  pre: 'mb-2 overflow-x-auto whitespace-pre rounded-lg bg-gray-100 p-2 font-mono text-xs last:mb-0 dark:bg-white/5',
  // The blinking caret `appendCaret` puts at the end of a reply that is still being written.
  span: 'ml-0.5 inline-block h-4 w-0.5 translate-y-0.5 animate-pulse bg-current motion-reduce:animate-none',
}

/** The classes of a code block that carries a copy button: the margin moves to the wrapper and the button gets room. */
export const MARKDOWN_COPYABLE_TAG_CLASSES: Readonly<Record<string, string>> = {
  ...MARKDOWN_TAG_CLASSES,
  pre: 'overflow-x-auto whitespace-pre rounded-lg bg-gray-100 p-2 pr-10 font-mono text-xs dark:bg-white/5',
}

/** The wrapper of a code block that carries a copy button, which anchors the button in the block's corner. */
export const MARKDOWN_COPYABLE_WRAPPER_CLASSES = 'relative mb-2 last:mb-0'

/** How deep emphasis may nest; past it the markers are shown as text, so recursion stays bounded. */
const MAX_INLINE_DEPTH = 4

/** A fence line, opening or closing. */
const FENCE_LINE = /^\s{0,3}```/

/**
 * A heading line with its trailing space already trimmed: its text is the first group. A lazy group before `\s*$` retries
 * at every space of a long line, which held the page for seconds on 80 KB; `s` lets the group take a U+2028 too, because
 * a `.` stopping there fails `$` and walks the spaces back the same way.
 */
const HEADING_LINE = /^\s{0,3}#{1,6}\s+(.+)$/s

/** A bullet line: its text is the first group. */
const BULLET_LINE = /^\s*[-*+]\s+(.+)$/s

/** A numbered line: its number and its text. Nine digits at most, so `start` is always a sane number. */
const NUMBERED_LINE = /^\s*(\d{1,9})[.)]\s+(.+)$/s

/** Characters a backslash makes literal. */
const ESCAPABLE = new Set(['\\', '*', '`'])

/**
 * Builds an element node.
 *
 * @param tag - The output tag, one of `MARKDOWN_TAG_CLASSES` or an emphasis tag.
 * @param children - Its content.
 * @param attrs - Its attributes, none by default.
 * @returns The node.
 */
const element = (tag: string, children: RichNode[], attrs: Record<string, string> = {}): RichElementNode => ({
  type: UiRichNodeType.Element,
  tag,
  attrs,
  children,
})

/**
 * Whether an emphasis pair may wrap what lies between its markers: `2 * 3 * 4` must stay arithmetic.
 *
 * @param text - The text holding the pair.
 * @param start - Where the content starts.
 * @param end - Where the content ends.
 * @returns True when the content is not empty and has no space at either edge.
 */
const wrapsContent = (text: string, start: number, end: number): boolean =>
  end > start && !/\s/.test(text.charAt(start)) && !/\s/.test(text.charAt(end - 1))

/**
 * Reads the inline constructs of one run of text.
 *
 * @param text - The text.
 * @param depth - How many emphasis levels surround it.
 * @returns Text and emphasis nodes.
 */
const parseInline = (text: string, depth = 0): RichNode[] => {
  const nodes: RichNode[] = []
  let plain = ''
  let at = 0

  /**
   * Moves the text collected so far into a node, ahead of an element.
   *
   * @returns Nothing.
   */
  const flush = (): void => {
    if (plain !== '') {
      nodes.push({ type: UiRichNodeType.Text, text: plain })
      plain = ''
    }
  }

  while (at < text.length) {
    const current = text.charAt(at)
    if (current === '\\' && ESCAPABLE.has(text.charAt(at + 1))) {
      plain += text.charAt(at + 1)
      at += 2
      continue
    }
    if (current === '`') {
      const close = text.indexOf('`', at + 1)
      if (close > at + 1) {
        flush()
        // Code is taken before emphasis and never read for markers: `**` inside it is what the reader should see.
        nodes.push(element('code', [{ type: UiRichNodeType.Text, text: text.slice(at + 1, close) }]))
        at = close + 1
        continue
      }
    }
    if (current === '*' && depth < MAX_INLINE_DEPTH) {
      const marker = text.startsWith('**', at) ? '**' : '*'
      const start = at + marker.length
      const close = text.indexOf(marker, start)
      if (close !== -1 && wrapsContent(text, start, close)) {
        flush()
        nodes.push(element(marker === '**' ? 'strong' : 'em', parseInline(text.slice(start, close), depth + 1)))
        at = close + marker.length
        continue
      }
    }
    plain += current
    at += 1
  }
  flush()
  return nodes
}

/**
 * Reads a reply into a tree `renderRichNodes` can draw.
 *
 * @param text - The reply as written, possibly still unfinished.
 * @returns Block nodes in order; empty for an empty reply.
 */
export const parseMarkdownText = (text: string): RichNode[] => {
  const blocks: RichNode[] = []
  const lines = text.replace(/\r\n?/g, '\n').split('\n')
  let paragraph: string[] = []
  let list: RichElementNode | null = null

  /**
   * Ends the paragraph and the list in progress, ahead of a block of another kind.
   *
   * @returns Nothing.
   */
  const closeOpenBlocks = (): void => {
    if (paragraph.length > 0) {
      // The lines stay joined by their line break: the bubble keeps it, as it did before replies were read as Markdown.
      blocks.push(element('p', parseInline(paragraph.join('\n'))))
      paragraph = []
    }
    list = null
  }

  /**
   * Adds an item to the list in progress, starting a list when none of that tag is open.
   *
   * @param tag - `ul` or `ol`.
   * @param content - The item's text.
   * @param attrs - The attributes of a list this item would start.
   * @returns Nothing.
   */
  const addItem = (tag: 'ul' | 'ol', content: string, attrs: Record<string, string> = {}): void => {
    if (paragraph.length > 0 || list?.tag !== tag) {
      closeOpenBlocks()
      list = element(tag, [], attrs)
      blocks.push(list)
    }
    list?.children.push(element('li', parseInline(content)))
  }

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index] ?? ''
    if (FENCE_LINE.test(line)) {
      closeOpenBlocks()
      const code: string[] = []
      index += 1
      while (index < lines.length && !FENCE_LINE.test(lines[index] ?? '')) {
        code.push(lines[index] ?? '')
        index += 1
      }
      blocks.push(element('pre', [{ type: UiRichNodeType.Text, text: code.join('\n') }]))
      continue
    }
    if (line.trim() === '') {
      closeOpenBlocks()
      continue
    }
    const heading = HEADING_LINE.exec(line.trimEnd())
    if (heading?.[1]) {
      closeOpenBlocks()
      // One level only: a reply's own `#` sits inside a page that already has its headings.
      blocks.push(element('h4', parseInline(heading[1])))
      continue
    }
    const bullet = BULLET_LINE.exec(line)
    if (bullet?.[1]) {
      addItem('ul', bullet[1])
      continue
    }
    const numbered = NUMBERED_LINE.exec(line)
    if (numbered?.[1] && numbered[2]) {
      const start = Number(numbered[1])
      addItem('ol', numbered[2], start === 1 ? {} : { start: String(start) })
      continue
    }
    list = null
    paragraph.push(line)
  }
  closeOpenBlocks()
  return blocks
}

/**
 * Puts the caret of a reply that is still being written at the end of its last line: inside the last paragraph, heading
 * or list item, and on a line of its own after a code block or an empty reply.
 *
 * @param blocks - The parsed reply; changed in place.
 * @returns The same blocks, for chaining.
 */
export const appendCaret = (blocks: RichNode[]): RichNode[] => {
  const caret = element('span', [], { 'aria-hidden': 'true' })
  const last = blocks.at(-1)
  const target = last?.type === UiRichNodeType.Element && (last.tag === 'ul' || last.tag === 'ol') ? last.children.at(-1) : last
  if (target?.type === UiRichNodeType.Element && target.tag !== 'pre') {
    target.children.push(caret)
  } else {
    blocks.push(element('p', [caret]))
  }
  return blocks
}
