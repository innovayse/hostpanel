/**
 * Unit tests for the safe Markdown reader: the supported blocks and inline pieces, and the guarantees that
 * matter for text from outside the panel (no HTML survives, only https links become links).
 */
import { describe, expect, it } from 'vitest'
import { isSafeLink, MarkdownBlockKind, MarkdownInlineKind, parseMarkdown } from '@/utils/markdown'

describe('parseMarkdown', () => {
  it('gives no blocks for null, empty and blank text', () => {
    expect(parseMarkdown(null)).toEqual([])
    expect(parseMarkdown('')).toEqual([])
    expect(parseMarkdown('  \n \n')).toEqual([])
  })

  it('reads headings, paragraphs and joins the lines of one paragraph', () => {
    const blocks = parseMarkdown('## Added\n\nFirst line\nsecond line')

    expect(blocks.map((block) => block.kind)).toEqual([MarkdownBlockKind.Heading, MarkdownBlockKind.Paragraph])
    expect(blocks[0]?.inlines[0]?.text).toBe('Added')
    expect(blocks[1]?.inlines[0]?.text).toBe('First line second line')
  })

  it('reads bullet and numbered lists, flattening indentation', () => {
    const [bullets, numbers] = parseMarkdown('- one\n  - nested\n* two\n\n1. first\n2) second')

    expect(bullets?.ordered).toBe(false)
    expect(bullets?.items.map((item) => item.inlines[0]?.text)).toEqual(['one', 'nested', 'two'])
    expect(numbers?.ordered).toBe(true)
    expect(numbers?.items).toHaveLength(2)
  })

  it('reads a quote, a rule and a fenced code block verbatim', () => {
    const blocks = parseMarkdown('> quoted\n> text\n\n---\n\n```\n<b>x</b>\n  indented\n```')

    expect(blocks.map((block) => block.kind)).toEqual([MarkdownBlockKind.Quote, MarkdownBlockKind.Rule, MarkdownBlockKind.Code])
    expect(blocks[0]?.inlines[0]?.text).toBe('quoted text')
    expect(blocks[2]?.code).toBe('<b>x</b>\n  indented')
  })

  it('runs an unclosed fence to the end of the text', () => {
    const [block] = parseMarkdown('```\nline one\nline two')

    expect(block?.kind).toBe(MarkdownBlockKind.Code)
    expect(block?.code).toBe('line one\nline two')
  })

  it('reads bold, italic, code and links inside a line', () => {
    const [block] = parseMarkdown('A **bold**, an *italic*, `code` and [docs](https://example.org/a) here')
    const kinds = block?.inlines.map((inline) => inline.kind)

    expect(kinds).toEqual([
      MarkdownInlineKind.Text,
      MarkdownInlineKind.Strong,
      MarkdownInlineKind.Text,
      MarkdownInlineKind.Emphasis,
      MarkdownInlineKind.Text,
      MarkdownInlineKind.Code,
      MarkdownInlineKind.Text,
      MarkdownInlineKind.Link,
      MarkdownInlineKind.Text,
    ])
    expect(block?.inlines[7]).toMatchObject({ text: 'docs', href: 'https://example.org/a' })
  })

  it('keeps raw HTML as plain text', () => {
    const [block] = parseMarkdown('<img src=x onerror=alert(1)> and <script>bad()</script>')

    expect(block?.inlines.every((inline) => inline.kind === MarkdownInlineKind.Text)).toBe(true)
    expect(block?.inlines.map((inline) => inline.text).join('')).toBe('<img src=x onerror=alert(1)> and <script>bad()</script>')
  })

  it('turns a link that is not https into the text as written', () => {
    const source = '[a](javascript:alert(1)) [b](http://example.org) [c](/relative)'
    const [block] = parseMarkdown(source)

    expect(block?.inlines.some((inline) => inline.kind === MarkdownInlineKind.Link)).toBe(false)
    expect(block?.inlines.map((inline) => inline.text).join('')).toBe(source)
  })

  it('does not read snake_case words as italic', () => {
    const [block] = parseMarkdown('use my_setting_name here')

    expect(block?.inlines).toHaveLength(1)
    expect(block?.inlines[0]?.kind).toBe(MarkdownInlineKind.Text)
  })

  it('gives every node its own id and reads line feed and carriage return endings alike', () => {
    const blocks = parseMarkdown('# A\r\n\r\n- b\r\n- c')
    const ids = blocks.flatMap((block) => [
      block.id,
      ...block.inlines.map((inline) => inline.id),
      ...block.items.flatMap((item) => [item.id, ...item.inlines.map((inline) => inline.id)]),
    ])

    expect(new Set(ids).size).toBe(ids.length)
    expect(blocks[1]?.items).toHaveLength(2)
  })
})

describe('isSafeLink', () => {
  it('accepts an absolute https address only', () => {
    expect(isSafeLink('https://example.org/x?y=1')).toBe(true)
    expect(isSafeLink('http://example.org')).toBe(false)
    expect(isSafeLink('javascript:alert(1)')).toBe(false)
    expect(isSafeLink('//example.org')).toBe(false)
    expect(isSafeLink('not a url')).toBe(false)
  })
})
