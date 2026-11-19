/**
 * Tests for the SSL monitoring group tables: every group has a title key that every shipped language defines, and a tone.
 */
import { describe, expect, it } from 'vitest'
import { MessageLanguage, messages } from '@/locales/messages'
import type { MessageTree } from '@/locales/messages'
import { SSL_GROUP_TITLE_KEYS, SSL_GROUP_TONES, SslGroupKind } from '@/types/reportssl'

/**
 * Reads a dotted key out of a language's messages.
 *
 * @param language - The language.
 * @param key - Dotted key, area first.
 * @returns The message, or undefined when the key is missing.
 */
const messageOf = (language: MessageLanguage, key: string): unknown =>
  key.split('.').reduce<unknown>((node, part) => (node as MessageTree | undefined)?.[part], messages[language])

describe('SslGroupKind tables', () => {
  it('uses the codes of the SslExpiryGroup enum of the API', () => {
    expect(Object.values(SslGroupKind)).toEqual(['Within30Days', 'Within90Days', 'Within180Days', 'Over180Days', 'NoSsl'])
  })

  it.each(Object.values(SslGroupKind))('%s has a title in every language and a tone', (kind) => {
    for (const language of Object.values(MessageLanguage)) {
      expect(typeof messageOf(language, SSL_GROUP_TITLE_KEYS[kind])).toBe('string')
    }
    expect(SSL_GROUP_TONES[kind]).toBeDefined()
  })
})
