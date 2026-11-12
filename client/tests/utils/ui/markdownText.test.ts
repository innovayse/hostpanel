/**
 * Unit tests for the Markdown reader: each construct of the subset, text that must stay literal, unfinished input while
 * a reply streams, and input that must not slow it down.
 *
 * @module tests/utils/ui/markdownText.test
 */

import { describe, expect, it } from 'vitest'
import { appendCaret, parseMarkdownText } from '~/utils/ui/markdownText'
import type { RichNode } from '~/utils/ui/richText'
import { UiRichNodeType } from '~/utils/ui/enums'

/** A line long enough that quadratic scanning takes tens of seconds, while a linear one takes milliseconds. */
const LONG_RUN = 200_000

/** Far above any linear parse on a loaded runner and far below the quadratic one, so the check does not flake. */
const LINEAR_BUDGET_MS = 3_000

/**
 * Writes a tree in a compact form, so an expectation reads like the output.
 *
 * @param nodes - The parsed nodes.
 * @returns Text as is, an element as `tag(children)`.
 */
const shape = (nodes: RichNode[]): string =>
  nodes.map((node) => (node.type === UiRichNodeType.Text ? node.text : `${node.tag}(${shape(node.children)})`)).join('')

describe('parseMarkdownText', () => {
  it.each([
    ['**bold** word', 'p(strong(bold) word)'],
    ['*it* word', 'p(em(it) word)'],
    ['**a *b* c**', 'p(strong(a em(b) c))'],
    ['run `npm i`', 'p(run code(npm i))'],
    ['`**x**`', 'p(code(**x**))'],
    ['# Title', 'h4(Title)'],
    ['###### **T**', 'h4(strong(T))'],
    ['- a\n- b', 'ul(li(a)li(b))'],
    ['* a\n+ **b**', 'ul(li(a)li(strong(b)))'],
    ['1. a\n2) b', 'ol(li(a)li(b))'],
    ['one\n\ntwo', 'p(one)p(two)'],
    ['one\ntwo', 'p(one\ntwo)'],
    ['intro\n- a\nafter', 'p(intro)ul(li(a))p(after)'],
    ['```\n**x**\n<b>\n```\nend', 'pre(**x**\n<b>)p(end)'],
    ['a\r\n\r\nb', 'p(a)p(b)'],
  ])('reads %j', (input, expected) => {
    expect(shape(parseMarkdownText(input))).toBe(expected)
  })

  it.each([
    ['2 * 3 * 4', 'p(2 * 3 * 4)'],
    ['** spaced **', 'p(** spaced **)'],
    ['\\*\\*not bold\\*\\*', 'p(**not bold**)'],
    ['snake_case_name', 'p(snake_case_name)'],
    ['[x](https://a.example)', 'p([x](https://a.example))'],
    ['![x](https://a.example/i.png)', 'p(![x](https://a.example/i.png))'],
  ])('keeps %j literal', (input, expected) => {
    expect(shape(parseMarkdownText(input))).toBe(expected)
  })

  it.each([
    ['**unfinished', 'p(**unfinished)'],
    ['`unfinished', 'p(`unfinished)'],
    ['```\ncode so far', 'pre(code so far)'],
    ['', ''],
  ])('stays readable on the unfinished %j', (input, expected) => {
    expect(shape(parseMarkdownText(input))).toBe(expected)
  })

  it('takes a list start from digits only and leaves the first item of a plain list without one', () => {
    const [started] = parseMarkdownText('3. c')
    const [plain] = parseMarkdownText('1. a')

    expect(started).toMatchObject({ tag: 'ol', attrs: { start: '3' } })
    expect(plain).toMatchObject({ tag: 'ol', attrs: {} })
  })

  it('bounds nesting and stays fast on a hostile reply', () => {
    const deep = `${'*a '.repeat(2000)}`
    const nested = `${'**a *'.repeat(200)}x${'* a**'.repeat(200)}`
    const started = performance.now()

    expect(() => parseMarkdownText(deep + nested + '*'.repeat(50000))).not.toThrow()
    expect(performance.now() - started).toBeLessThan(2000)
  })

  it.each([
    ['a heading with a long run of inner spaces', `# a${' '.repeat(LONG_RUN)}b`],
    ['a heading with a long run of trailing spaces', `# a${' '.repeat(LONG_RUN)}`],
    ['a heading with a line separator past the spaces', `# ${' '.repeat(LONG_RUN)}\u2028b`],
    ['a bullet with a line separator past the spaces', `- ${' '.repeat(LONG_RUN)}\u2028b`],
    ['a numbered line with a line separator past the spaces', `1. ${' '.repeat(LONG_RUN)}\u2028b`],
  ])('reads %s in linear time', (_name, line) => {
    const started = performance.now()

    expect(parseMarkdownText(line)).toHaveLength(1)
    expect(performance.now() - started).toBeLessThan(LINEAR_BUDGET_MS)
  })

  it('still trims a heading and reads its text', () => {
    expect(shape(parseMarkdownText('## Title   '))).toBe('h4(Title)')
  })
})

describe('appendCaret', () => {
  it.each([
    ['text so far', 'p(text so farspan())'],
    ['# Plan', 'h4(Planspan())'],
    ['- one\n- two', 'ul(li(one)li(twospan()))'],
    ['```\ncode\n```', 'pre(code)p(span())'],
    ['', 'p(span())'],
  ])('puts the caret at the end of %j', (text, expected) => {
    expect(shape(appendCaret(parseMarkdownText(text)))).toBe(expected)
  })

  it('marks the caret hidden from assistive technology', () => {
    const caret = appendCaret(parseMarkdownText('x'))[0]

    expect(caret?.type === UiRichNodeType.Element && caret.children.at(-1)).toMatchObject({ tag: 'span', attrs: { 'aria-hidden': 'true' } })
  })
})
