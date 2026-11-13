/**
 * Tests for the listbox rules in `utils/ui/select`: the search filter, the highlight keys, the
 * printable-key check, the typed-word jump and the option ids.
 */
import { describe, expect, it } from 'vitest'
import { filterOptions, isPrintableKey, optionDomId, stepActive, typeaheadIndex } from '@/utils/ui/select'
import type { UiSelectOption } from '@/utils/ui/select'

/** Options used by the tests: one with extra search text and one dim "none" row. */
const options: UiSelectOption<string>[] = [
  { value: '', label: 'Pick one', muted: true },
  { value: 'a', label: 'Alpha', keywords: 'first 7' },
  { value: 'b', label: 'Beta', displayLabel: 'Beta long', description: 'second' },
]

describe('filterOptions', () => {
  it('keeps everything for a blank term', () => {
    expect(filterOptions(options, '  ')).toHaveLength(3)
  })

  it('matches label, long label, description and keywords, ignoring case', () => {
    expect(filterOptions(options, 'ALP').map(o => o.value)).toEqual(['a'])
    expect(filterOptions(options, 'long').map(o => o.value)).toEqual(['b'])
    expect(filterOptions(options, 'second').map(o => o.value)).toEqual(['b'])
    expect(filterOptions(options, '7').map(o => o.value)).toEqual(['a'])
  })

  it('drops the dim row as soon as a term is typed, even when it would match', () => {
    expect(filterOptions(options, 'pick')).toEqual([])
  })

  it('only drops dim rows when the owner already matched the term', () => {
    expect(filterOptions(options, 'zzz', false).map(o => o.value)).toEqual(['a', 'b'])
  })
})

describe('stepActive', () => {
  it('moves down and up and stops at both ends', () => {
    expect(stepActive(0, 3, 'ArrowDown')).toBe(1)
    expect(stepActive(2, 3, 'ArrowDown')).toBe(2)
    expect(stepActive(1, 3, 'ArrowUp')).toBe(0)
    expect(stepActive(0, 3, 'ArrowUp')).toBe(0)
  })

  it('jumps to the first and last row', () => {
    expect(stepActive(1, 3, 'Home')).toBe(0)
    expect(stepActive(1, 3, 'End')).toBe(2)
  })

  it('answers -1 for an empty list and keeps the index for another key', () => {
    expect(stepActive(0, 0, 'ArrowDown')).toBe(-1)
    expect(stepActive(1, 3, 'x')).toBe(1)
  })
})

describe('isPrintableKey', () => {
  it('accepts a character and refuses control keys and shortcuts', () => {
    expect(isPrintableKey({ key: 'a', ctrlKey: false, altKey: false, metaKey: false })).toBe(true)
    expect(isPrintableKey({ key: 'Enter', ctrlKey: false, altKey: false, metaKey: false })).toBe(false)
    expect(isPrintableKey({ key: 'a', ctrlKey: true, altKey: false, metaKey: false })).toBe(false)
  })
})

describe('typeaheadIndex', () => {
  const labels = ['Alpha', 'Beta', 'Bravo', 'Gamma']

  it('finds the next label that starts with the word, wrapping round', () => {
    expect(typeaheadIndex(labels, 'b', -1)).toBe(1)
    expect(typeaheadIndex(labels, 'b', 1)).toBe(2)
    expect(typeaheadIndex(labels, 'b', 2)).toBe(1)
  })

  it('narrows with a longer word and answers -1 for no match', () => {
    expect(typeaheadIndex(labels, 'br', 0)).toBe(2)
    expect(typeaheadIndex(labels, 'zz', 0)).toBe(-1)
    expect(typeaheadIndex([], 'a', -1)).toBe(-1)
  })
})

describe('optionDomId', () => {
  it('derives a row id from the list id and the index', () => {
    expect(optionDomId('list', 3)).toBe('list-option-3')
  })
})
