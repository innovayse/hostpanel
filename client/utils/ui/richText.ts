/**
 * Allow-list tokenizer for operator-authored HTML (knowledge-base articles, announcements).
 *
 * The body of such a record is HTML written in the admin portal. It is never handed to the
 * browser as markup: this module reads the string into a tree of known tags and attributes and
 * `UiRichText` turns that tree into Vue nodes. Anything not on the lists below is dropped:
 * unknown tags are unwrapped (their text stays), dangerous containers (`script`, `style`,
 * `iframe`, `svg` and the like) are dropped with their content, every `on*`/`style`/`class` attribute is
 * dropped, and a link or image URL must use a safe scheme. The text itself is decoded here and
 * rendered as a text node, so Vue escapes it; there is no path by which a string becomes markup.
 *
 * Pure functions, no DOM: the same code runs on the server and in the browser, so SSR output and
 * hydration agree.
 *
 * @module utils/ui/richText
 */

import { h } from 'vue'
import type { VNode } from 'vue'
import { isSafeUrl } from '~/utils/safeUrl'
import { UiRichNodeType } from '~/utils/ui/enums'

/** A text run of the parsed document. */
export interface RichTextNode {
  /** Discriminator. */
  type: UiRichNodeType.Text
  /** Decoded text; rendered as a Vue text node, so it is escaped on output. */
  text: string
}

/** An element of the parsed document. */
export interface RichElementNode {
  /** Discriminator. */
  type: UiRichNodeType.Element
  /** Lower-case tag name from the allow-list. */
  tag: string
  /** Attributes that survived the allow-list, already validated. */
  attrs: Record<string, string>
  /** Child nodes. */
  children: RichNode[]
}

/** Any node of the parsed document. */
export type RichNode = RichTextNode | RichElementNode

/** Tags that are kept. Everything else is unwrapped. */
export const ALLOWED_TAGS: ReadonlySet<string> = new Set([
  'p', 'br', 'hr', 'strong', 'b', 'em', 'i', 'u', 's', 'small', 'sub', 'sup', 'mark',
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'ul', 'ol', 'li', 'blockquote', 'code', 'pre', 'a', 'img',
  'table', 'thead', 'tbody', 'tfoot', 'tr', 'th', 'td', 'div', 'span',
])

/** Tags that have no content and no closing tag. */
const VOID_TAGS: ReadonlySet<string> = new Set(['br', 'hr', 'img'])

/** Containers dropped together with everything inside them. */
const DROP_WITH_CONTENT: ReadonlySet<string> = new Set([
  'script', 'style', 'iframe', 'frame', 'frameset', 'object', 'embed', 'applet', 'svg', 'math', 'template',
  'noscript', 'textarea', 'select', 'title', 'head', 'form', 'canvas', 'audio', 'video',
])

/** Attributes kept per tag; a tag not listed keeps none. */
const ALLOWED_ATTRIBUTES: Readonly<Record<string, readonly string[]>> = {
  a: ['href', 'title'],
  img: ['src', 'alt', 'title'],
  td: ['colspan', 'rowspan'],
  th: ['colspan', 'rowspan'],
  ol: ['start'],
}

/** URL schemes a link may use. */
const LINK_SCHEMES: ReadonlySet<string> = new Set(['http', 'https', 'mailto', 'tel'])

/** The `rel` of an off-site link from content: no opener, no referrer, no endorsement, user content. */
export const EXTERNAL_LINK_REL = 'noopener noreferrer nofollow ugc'

/**
 * URL schemes an image may use (no `data:`).
 *
 * Any `http(s)` host passes this allow-list, but the browser only loads what the page's
 * Content-Security-Policy `img-src` directive permits. The deployment's `img-src` is therefore
 * the second gate: an image on a host it does not list is refused by the browser, not by this
 * module. Images are deliberately not proxied through this app.
 */
const IMAGE_SCHEMES: ReadonlySet<string> = new Set(['http', 'https'])

/** Elements that implicitly close a still-open `p`, as a browser's parser would. */
const CLOSES_PARAGRAPH: ReadonlySet<string> = new Set([
  'p', 'div', 'ul', 'ol', 'li', 'table', 'blockquote', 'pre', 'hr', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
])

/** For an opening tag, the open tags directly above it that it closes (`li` closes `li`). */
const IMPLICIT_CLOSE: Readonly<Record<string, readonly string[]>> = {
  li: ['li'],
  td: ['td', 'th'],
  th: ['td', 'th'],
  tr: ['td', 'th', 'tr'],
  thead: ['td', 'th', 'tr', 'thead', 'tbody', 'tfoot'],
  tbody: ['td', 'th', 'tr', 'thead', 'tbody', 'tfoot'],
  tfoot: ['td', 'th', 'tr', 'thead', 'tbody', 'tfoot'],
}

/** Deepest nesting honoured; deeper tags are unwrapped so a hostile body cannot blow the stack. */
const MAX_DEPTH = 32

/** Literal classes each output tag carries, since the kit does not rely on a typography plugin. */
export const TAG_CLASSES: Readonly<Record<string, string>> = {
  p: 'mb-4 leading-relaxed',
  h1: 'mt-8 mb-4 text-3xl font-bold text-gray-900 dark:text-white',
  h2: 'mt-8 mb-3 text-2xl font-bold text-gray-900 dark:text-white',
  h3: 'mt-6 mb-3 text-xl font-semibold text-gray-900 dark:text-white',
  h4: 'mt-6 mb-2 text-lg font-semibold text-gray-900 dark:text-white',
  h5: 'mt-4 mb-2 text-base font-semibold text-gray-900 dark:text-white',
  h6: 'mt-4 mb-2 text-sm font-semibold uppercase tracking-wide text-gray-700 dark:text-gray-300',
  ul: 'mb-4 list-disc space-y-1 pl-6',
  ol: 'mb-4 list-decimal space-y-1 pl-6',
  li: 'leading-relaxed',
  blockquote: 'mb-4 border-l-4 border-primary-500/50 pl-4 italic text-gray-600 dark:text-gray-400',
  code: 'rounded bg-gray-100 px-1.5 py-0.5 font-mono text-sm dark:bg-white/10',
  pre: 'mb-4 overflow-x-auto rounded-xl bg-gray-100 p-4 font-mono text-sm dark:bg-white/5',
  a: 'text-primary-600 underline hover:text-primary-700 dark:text-primary-400 dark:hover:text-primary-300',
  img: 'my-4 h-auto max-w-full rounded-xl',
  hr: 'my-8 border-gray-200 dark:border-white/10',
  table: 'mb-4 w-full border-collapse text-sm',
  th: 'border border-gray-200 bg-gray-50 p-2 text-left font-semibold dark:border-white/10 dark:bg-white/5',
  td: 'border border-gray-200 p-2 dark:border-white/10',
}

/** Named character references the decoder understands. */
const NAMED_ENTITIES: Readonly<Record<string, string>> = {
  amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', ndash: '–', mdash: '—',
  hellip: '…', laquo: '«', raquo: '»', copy: '©', reg: '®', trade: '™',
  lsquo: '‘', rsquo: '’', ldquo: '“', rdquo: '”', bull: '•', middot: '·',
}

/** Matches one opening or closing tag (sticky, so it is anchored at `lastIndex`). */
const TAG_PATTERN = /<(\/?)([a-zA-Z][a-zA-Z0-9]*)((?:"[^"]*"|'[^']*'|[^>"'])*)>/y

/** Matches one attribute inside a tag's attribute text. */
const ATTRIBUTE_PATTERN = /([^\s"'<>/=]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'=<>`]+)))?/g

/**
 * Decodes the character references in a string.
 *
 * @param input Text that may contain `&amp;`, `&#39;` or `&#x27;` style references.
 * @returns The decoded text; unknown or invalid references are left as written.
 */
export const decodeEntities = (input: string): string =>
  input.replace(/&(#x[0-9a-fA-F]+|#[0-9]+|[a-zA-Z]+);/g, (whole, body: string) => {
    if (body.startsWith('#')) {
      const code = body[1] === 'x' || body[1] === 'X' ? parseInt(body.slice(2), 16) : parseInt(body.slice(1), 10)
      const isSurrogate = code >= 0xd800 && code <= 0xdfff
      if (!Number.isFinite(code) || code <= 0 || code > 0x10ffff || isSurrogate) {
        return whole
      }
      return String.fromCodePoint(code)
    }
    return NAMED_ENTITIES[body.toLowerCase()] ?? whole
  })

/**
 * Reads the attributes of one tag and keeps only the allowed, valid ones.
 *
 * @param tag Lower-case tag name.
 * @param raw The text between the tag name and `>`.
 * @returns The attributes to render.
 */
const readAttributes = (tag: string, raw: string): Record<string, string> => {
  const allowed = ALLOWED_ATTRIBUTES[tag]
  const attrs: Record<string, string> = {}
  if (!allowed) {
    return attrs
  }
  for (const match of raw.matchAll(ATTRIBUTE_PATTERN)) {
    const name = (match[1] ?? '').toLowerCase()
    if (!allowed.includes(name)) {
      continue
    }
    const value = decodeEntities(match[2] ?? match[3] ?? match[4] ?? '')
    if (name === 'href' && !isSafeUrl(value, LINK_SCHEMES)) {
      continue
    }
    if (name === 'src' && !isSafeUrl(value, IMAGE_SCHEMES)) {
      continue
    }
    if ((name === 'colspan' || name === 'rowspan' || name === 'start') && !/^\d{1,4}$/.test(value)) {
      continue
    }
    attrs[name] = value
  }
  if (tag === 'a' && attrs.href && /^(https?:)?\/\//i.test(attrs.href)) {
    // An off-site link must not hand the opener window to the target, and it is user-supplied
    // content, so search engines are told not to vouch for it.
    attrs.target = '_blank'
    attrs.rel = EXTERNAL_LINK_REL
  }
  return attrs
}

/**
 * Parses operator HTML into a tree that contains only allow-listed tags and attributes.
 *
 * @param html The raw body from the API.
 * @returns The sanitised tree; empty for an empty or whitespace-only input.
 */
export const parseRichText = (html: string): RichNode[] => {
  const root: RichNode[] = []
  /** Open elements, innermost last; the root list is the parent of the first. */
  const stack: RichElementNode[] = []
  const lower = html.toLowerCase()
  let index = 0

  /**
   * The list a new node is appended to.
   *
   * @returns The children of the innermost open element, or the root list.
   */
  const currentChildren = (): RichNode[] => (stack.at(-1)?.children ?? root)

  /**
   * Appends decoded text, merging with a preceding text node.
   *
   * @param raw Undecoded text.
   */
  const pushText = (raw: string): void => {
    if (raw === '') {
      return
    }
    const text = decodeEntities(raw)
    const children = currentChildren()
    const last = children[children.length - 1]
    if (last && last.type === UiRichNodeType.Text) {
      last.text += text
      return
    }
    children.push({ type: UiRichNodeType.Text, text })
  }

  /**
   * Closes the innermost open element if it is one of the given tags.
   *
   * @param tags Tags that may be closed.
   * @returns True when an element was closed.
   */
  const closeTopIfIn = (tags: readonly string[]): boolean => {
    const top = stack[stack.length - 1]
    if (top && tags.includes(top.tag)) {
      stack.pop()
      return true
    }
    return false
  }

  while (index < html.length) {
    const lt = html.indexOf('<', index)
    if (lt === -1) {
      pushText(html.slice(index))
      break
    }
    pushText(html.slice(index, lt))

    if (html.startsWith('<!--', lt)) {
      const end = html.indexOf('-->', lt + 4)
      index = end === -1 ? html.length : end + 3
      continue
    }

    TAG_PATTERN.lastIndex = lt
    const match = TAG_PATTERN.exec(html)
    if (!match) {
      if (html.startsWith('<!', lt) || html.startsWith('<?', lt)) {
        const end = html.indexOf('>', lt)
        index = end === -1 ? html.length : end + 1
      } else {
        pushText('<')
        index = lt + 1
      }
      continue
    }

    index = lt + match[0].length
    const closing = match[1] === '/'
    const tag = (match[2] ?? '').toLowerCase()
    const attributeText = match[3] ?? ''

    if (closing) {
      const depth = stack.map((element) => element.tag).lastIndexOf(tag)
      if (depth !== -1) {
        stack.length = depth
      }
      continue
    }

    if (DROP_WITH_CONTENT.has(tag)) {
      if (!attributeText.trimEnd().endsWith('/')) {
        const end = lower.indexOf(`</${tag}`, index)
        if (end === -1) {
          index = html.length
        } else {
          const close = html.indexOf('>', end)
          index = close === -1 ? html.length : close + 1
        }
      }
      continue
    }

    if (!ALLOWED_TAGS.has(tag) || stack.length >= MAX_DEPTH) {
      continue
    }

    if (CLOSES_PARAGRAPH.has(tag)) {
      closeTopIfIn(['p'])
    }
    const implicit = IMPLICIT_CLOSE[tag]
    if (implicit) {
      while (closeTopIfIn(implicit)) {
        // keep closing while the element above is one this tag supersedes
      }
    }

    const element: RichElementNode = { type: UiRichNodeType.Element, tag, attrs: readAttributes(tag, attributeText), children: [] }
    currentChildren().push(element)
    if (!VOID_TAGS.has(tag)) {
      stack.push(element)
    }
  }

  return root
}

/**
 * Converts one parsed node into a VNode, the one place such a tree becomes something the browser draws.
 *
 * @param node - A node of a parsed document.
 * @param tagClasses - The classes each output tag carries.
 * @returns The VNode (a text node for text).
 */
export const renderRichNode = (node: RichNode, tagClasses: Readonly<Record<string, string>>): VNode | string => {
  if (node.type === UiRichNodeType.Text) {
    return node.text
  }
  const props: Record<string, string> = { ...node.attrs }
  const classes = tagClasses[node.tag]
  if (classes) {
    props.class = classes
  }
  return h(node.tag, props, node.children.map((child) => renderRichNode(child, tagClasses)))
}
