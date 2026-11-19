/**
 * Tests for the client section list: the tab strip and the client layout's skeleton are made from it, so it has to be
 * complete, unambiguous and in the strip's order.
 */
import { describe, expect, it } from 'vitest'
import { CLIENT_NAV, ClientNavKey } from '@/lib/clientSections'
import { ClientSection } from '@/lib/routes'
import { translate } from '@/lib/translate'

describe('CLIENT_NAV', () => {
  it('lists every section key once, summary first and the log last', () => {
    const keys = CLIENT_NAV.map(entry => entry.key)

    expect(keys).toHaveLength(Object.values(ClientNavKey).length)
    expect(new Set(keys).size).toBe(keys.length)
    expect(keys[0]).toBe(ClientNavKey.Summary)
    expect(keys.at(-1)).toBe(ClientNavKey.Log)
  })

  it('opens a different route segment from each entry', () => {
    const sections = CLIENT_NAV.map(entry => entry.section)

    expect(new Set(sections).size).toBe(sections.length)
    expect(sections[0]).toBe(ClientSection.Summary)
  })

  it('gives every entry a caption that resolves in the catalogue', () => {
    for (const entry of CLIENT_NAV) {
      expect(translate(entry.label)).not.toBe(entry.label)
    }
  })
})
