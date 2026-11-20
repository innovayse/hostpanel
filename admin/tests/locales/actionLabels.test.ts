/**
 * Locale lint: an action label never starts with a plus sign. Every action button draws its own plus icon
 * (`UiActionKind`), so a literal "+ " or "(+) " in the string would print the plus twice.
 */
import { describe, expect, it } from 'vitest'
import { messages } from '@/locales/messages'

/** A string that opens with a plus sign used as a bullet: `+ Add`, `(+) Add`. A phone number such as `+1 555` has no space after the plus and is left alone. */
const LEADING_PLUS = /^\s*(?:\(\+\)|\+)\s+\S/

/**
 * Flattens a nested message tree into dotted keys and their string values.
 *
 * @param node - A message object, a string or anything else a catalogue may hold.
 * @param path - The dotted key of the node.
 * @returns Every string leaf as `[key, value]`.
 */
const leaves = (node: unknown, path = ''): [string, string][] => {
  if (typeof node === 'string') {
    return [[path, node]]
  }
  if (node !== null && typeof node === 'object') {
    return Object.entries(node).flatMap(([key, value]) => leaves(value, path === '' ? key : `${path}.${key}`))
  }
  return []
}

describe('action labels', () => {
  it('recognises a leading plus bullet and leaves phone numbers alone', () => {
    expect(LEADING_PLUS.test('+ Add Item')).toBe(true)
    expect(LEADING_PLUS.test('(+) Create New')).toBe(true)
    expect(LEADING_PLUS.test('+1 555 000 0000')).toBe(false)
    expect(LEADING_PLUS.test('Add Item')).toBe(false)
  })

  it.each(Object.keys(messages))('has no label starting with a plus sign in %s', (locale) => {
    const offenders = leaves(messages[locale as keyof typeof messages])
      .filter(([, value]) => LEADING_PLUS.test(value))
      .map(([key]) => key)

    expect(offenders).toEqual([])
  })
})
