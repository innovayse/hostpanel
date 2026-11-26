/**
 * Unit tests for `utils/domainName.ts`, the browser's copy of the API's `DomainNameRules`.
 *
 * The name tables below are the ones in the API's `DomainNameRulesTests`, case for case: the two
 * rules are one rule, so a name added to either table is added to the other.
 *
 * @module tests/utils/domainName.test
 */

import { describe, expect, it } from 'vitest'
import {
  MAX_DOMAIN_LABEL_LENGTH,
  MAX_DOMAIN_NAME_LENGTH,
  isValidDomainBase,
  isValidDomainExtension,
  isValidDomainLabel,
  isValidDomainName,
} from '~/utils/domainName'

/** Ordinary names, a nested one, a hyphen inside a label, digits and an encoded internationalised label. */
const WELL_FORMED_NAMES = [
  'example.com',
  'shop.example.co.uk',
  'my-shop.com',
  '123.io',
  'xn--bcher-kva.de',
  'EXAMPLE.COM',
]

/** Text that is no name at all. */
const MALFORMED_NAMES = [
  'bad name!.com',
  'bad name.com',
  '-bad-.com',
  '-bad.com',
  'bad-.com',
  'bad_name.com',
  'bad!.com',
  'example',
  'example.',
  '.com',
  'exa..mple.com',
  'example.-com',
  'example.com/path',
  'münchen.de',
  'example.münchen',
  'example.123',
  'example.c',
  'example.c0m',
  'example.xn--p1ai',
  '  example.com  ',
  '',
  '   ',
]

describe('isValidDomainName', () => {
  it.each(WELL_FORMED_NAMES)('accepts the well-formed name %j', (name) => {
    expect(isValidDomainName(name)).toBe(true)
  })

  it.each(MALFORMED_NAMES)('refuses the malformed name %j', (name) => {
    expect(isValidDomainName(name)).toBe(false)
  })

  it('holds the rule registration holds', () => {
    expect(isValidDomainName('shop.example.com')).toBe(true)
    expect(isValidDomainName('a.co')).toBe(true)
    expect(isValidDomainName('example.com ')).toBe(false)
    expect(isValidDomainName('example.com\n')).toBe(false)
  })

  it('refuses a missing text', () => {
    expect(isValidDomainName(null)).toBe(false)
    expect(isValidDomainName(undefined)).toBe(false)
  })

  it('holds a label to sixty three characters', () => {
    const longest = 'a'.repeat(MAX_DOMAIN_LABEL_LENGTH)
    const tooLong = 'a'.repeat(MAX_DOMAIN_LABEL_LENGTH + 1)

    expect(isValidDomainName(`${longest}.com`)).toBe(true)
    expect(isValidDomainName(`${tooLong}.com`)).toBe(false)
  })

  it('holds the whole name to the limit even when each label is short enough', () => {
    const label = 'a'.repeat(50)
    const within = [label, label, label, label, 'com'].join('.')
    const over = [label, label, label, label, label, 'com'].join('.')

    expect(within.length).toBeLessThanOrEqual(MAX_DOMAIN_NAME_LENGTH)
    expect(over.length).toBeGreaterThan(MAX_DOMAIN_NAME_LENGTH)
    expect(isValidDomainName(within)).toBe(true)
    expect(isValidDomainName(over)).toBe(false)
  })
})

describe('isValidDomainLabel', () => {
  it('judges one label on its own: hyphens only inside, 1 to 63 characters', () => {
    expect(isValidDomainLabel('a-b')).toBe(true)
    expect(isValidDomainLabel('a')).toBe(true)
    expect(isValidDomainLabel('')).toBe(false)
    expect(isValidDomainLabel('-a')).toBe(false)
    expect(isValidDomainLabel('a-')).toBe(false)
    expect(isValidDomainLabel('a b')).toBe(false)
    expect(isValidDomainLabel('münchen')).toBe(false)
  })
})

describe('isValidDomainExtension', () => {
  it('needs two or more letters and never a number or a mix of letters and digits', () => {
    expect(isValidDomainExtension('com')).toBe(true)
    expect(isValidDomainExtension('io')).toBe(true)
    expect(isValidDomainExtension('c')).toBe(false)
    expect(isValidDomainExtension('123')).toBe(false)
    expect(isValidDomainExtension('c0m')).toBe(false)
    expect(isValidDomainExtension('xn--p1ai')).toBe(false)
    expect(isValidDomainExtension('')).toBe(false)
  })
})

describe('isValidDomainBase', () => {
  it.each(['example', 'my-shop', '123', 'shop.example', 'EXAMPLE'])('accepts the text before an extension %j', (base) => {
    expect(isValidDomainBase(base)).toBe(true)
  })

  it.each(['bad-name!', 'bad name', '-bad-', 'bad_name', 'exa..mple', '.example', 'example.', 'münchen', '', 'a'.repeat(MAX_DOMAIN_LABEL_LENGTH + 1)])(
    'refuses the text before an extension %j',
    (base) => {
      expect(isValidDomainBase(base)).toBe(false)
    },
  )
})
