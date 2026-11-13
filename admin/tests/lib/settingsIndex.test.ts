/**
 * Tests for the settings index catalogue: every card has its text in every language and no
 * section is listed twice.
 */
import { describe, expect, it } from 'vitest'
import { SETTINGS_INDEX } from '@/lib/settingsIndex'
import { valueAtPath } from '@/lib/valueAtPath'
import { MessageLanguage, messages } from '@/locales/messages'

describe('SETTINGS_INDEX', () => {
  it('names every group and every card in every language', () => {
    for (const language of Object.values(MessageLanguage)) {
      for (const { group, entries } of SETTINGS_INDEX) {
        expect(typeof valueAtPath(messages[language], `settings.index.groups.${group}`), `${language}: ${group}`).toBe('string')
        for (const { item } of entries) {
          expect(typeof valueAtPath(messages[language], `settings.index.items.${item}.title`), `${language}: ${item}`).toBe('string')
          expect(typeof valueAtPath(messages[language], `settings.index.items.${item}.description`), `${language}: ${item}`).toBe('string')
        }
      }
    }
  })

  it('lists no section twice', () => {
    const items = SETTINGS_INDEX.flatMap((group) => group.entries.map((entry) => entry.item))

    expect(new Set(items).size).toBe(items.length)
  })
})
