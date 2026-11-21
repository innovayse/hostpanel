/**
 * Tests for `parseLegalText`: the operator's plain text becomes sections of plain strings.
 *
 * @module tests/utils/legalText.test
 */

import { describe, expect, it } from 'vitest'
import { parseLegalText } from '~/utils/legalText'

/** The heading given to text written before the first section. */
const INTRO = 'Terms of Service'

describe('parseLegalText', () => {
  it('gives no sections for an empty or blank text', () => {
    expect(parseLegalText('', INTRO)).toEqual([])
    expect(parseLegalText('  \n \r\n\n ', INTRO)).toEqual([])
  })

  it('opens a section at each ## line and numbers their ids', () => {
    const sections = parseLegalText('## Seller\nAcme Ltd.\n\n## Payment\nBy card.', INTRO)

    expect(sections.map(section => [section.id, section.title])).toEqual([['custom-1', 'Seller'], ['custom-2', 'Payment']])
    expect(sections[0]?.paragraphs).toEqual(['Acme Ltd.'])
    expect(sections[1]?.paragraphs).toEqual(['By card.'])
  })

  it('starts a new paragraph at a blank line and joins the lines of one paragraph', () => {
    const [section] = parseLegalText('## Terms\nFirst line\nsecond line.\n\n  \nSecond paragraph.', INTRO)

    expect(section?.paragraphs).toEqual(['First line second line.', 'Second paragraph.'])
  })

  it('reads - lines as list entries, after the paragraphs', () => {
    const [section] = parseLegalText('## Rules\n- No spam\nIntro text.\n- No abuse', INTRO)

    expect(section?.items).toEqual(['No spam', 'No abuse'])
    expect(section?.paragraphs).toEqual(['Intro text.'])
  })

  it('puts text before the first heading in a section titled by the intro', () => {
    const sections = parseLegalText('Plain opening.\n\n## Later\nMore.', INTRO)

    expect(sections.map(section => section.title)).toEqual([INTRO, 'Later'])
    expect(sections[0]?.paragraphs).toEqual(['Plain opening.'])
  })

  it('reads a text with no heading as one section', () => {
    const sections = parseLegalText('Only text.', INTRO)

    expect(sections).toHaveLength(1)
    expect(sections[0]).toMatchObject({ id: 'custom-1', title: INTRO, paragraphs: ['Only text.'] })
  })

  it('handles Windows line breaks', () => {
    const sections = parseLegalText('## A\r\nOne.\r\n\r\nTwo.', INTRO)

    expect(sections[0]?.paragraphs).toEqual(['One.', 'Two.'])
  })

  it('keeps markup as plain text, never interpreting it', () => {
    const [section] = parseLegalText('## <b>Bold</b>\n<script>alert(1)</script> **x** [a](http://x)', INTRO)

    expect(section?.title).toBe('<b>Bold</b>')
    expect(section?.paragraphs).toEqual(['<script>alert(1)</script> **x** [a](http://x)'])
  })

  it('does not treat ## without a following space or a longer hash run as a heading', () => {
    const [section] = parseLegalText('##NotAHeading\n### Also not', INTRO)

    expect(section?.title).toBe(INTRO)
    expect(section?.paragraphs).toEqual(['##NotAHeading ### Also not'])
  })
})
