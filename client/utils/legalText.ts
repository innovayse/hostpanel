/**
 * Turns the plain text an operator writes for a legal page into the sections `LegalDocument` lays out.
 *
 * The format is deliberately small, so nothing the operator types is ever run as markup: a line that
 * starts with `## ` opens a section and names it, a blank line ends a paragraph, and a line that
 * starts with `- ` is a list entry. Everything else is paragraph text. The result is plain strings
 * that the page renders as text, never as HTML.
 *
 * @module utils/legalText
 */

import type { LegalSection } from '~/types/legalsection'

/** What a line that opens a section starts with. */
const HEADING_PREFIX = '## '

/** What a line that is a list entry starts with. */
const ITEM_PREFIX = '- '

/** The section being filled while the text is read. */
interface SectionDraft {
  /** The heading of the section. */
  title: string
  /** The finished paragraphs. */
  paragraphs: string[]
  /** The list entries. */
  items: string[]
  /** The lines of the paragraph still being written. */
  pending: string[]
}

/**
 * Creates an empty section draft.
 *
 * @param title The heading of the section.
 * @returns The draft.
 */
const draftOf = (title: string): SectionDraft => ({ title, paragraphs: [], items: [], pending: [] })

/**
 * Ends the paragraph being written, if it has any text.
 *
 * @param draft The section whose paragraph ends.
 */
const endParagraph = (draft: SectionDraft): void => {
  if (draft.pending.length > 0) {
    draft.paragraphs.push(draft.pending.join(' '))
    draft.pending = []
  }
}

/**
 * Reads an operator's legal text into sections.
 *
 * Text written before the first `## ` line becomes a first section headed by `introTitle`; a text
 * with no `## ` line at all is that one section. Paragraphs come before the list entries inside a
 * section, whatever order they were written in.
 *
 * @param text The operator's text; blank or whitespace-only gives no sections.
 * @param introTitle The heading of text that comes before the first section heading.
 * @returns The sections in reading order, each with a stable `custom-<n>` id; empty when there is no text.
 */
export const parseLegalText = (text: string, introTitle: string): LegalSection[] => {
  const drafts: SectionDraft[] = []
  let current: SectionDraft | undefined

  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim()

    if (line.startsWith(HEADING_PREFIX)) {
      if (current) {
        endParagraph(current)
      }
      current = draftOf(line.slice(HEADING_PREFIX.length).trim())
      drafts.push(current)
      continue
    }

    if (line === '') {
      if (current) {
        endParagraph(current)
      }
      continue
    }

    if (!current) {
      current = draftOf(introTitle)
      drafts.push(current)
    }

    if (line.startsWith(ITEM_PREFIX)) {
      endParagraph(current)
      current.items.push(line.slice(ITEM_PREFIX.length).trim())
    } else {
      current.pending.push(line)
    }
  }

  return drafts.map((draft, index) => {
    endParagraph(draft)
    return { id: `custom-${index + 1}`, title: draft.title, paragraphs: draft.paragraphs, items: draft.items }
  })
}
