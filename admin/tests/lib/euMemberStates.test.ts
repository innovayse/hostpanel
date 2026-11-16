/**
 * Tests for the EU member state list the VAT rates form offers: it holds the 27 member states once each, and it is
 * the same set the API accepts, read from the backend list so the two cannot drift apart.
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { EU_MEMBER_STATES } from '@/lib/euMemberStates'

/** The backend list of member states the API checks a VAT rate row against. */
const BACKEND_LIST = join(
  __dirname,
  '..',
  '..',
  '..',
  'backend',
  'src',
  'Innovayse.Hostpanel.Application',
  'Billing',
  'Common',
  'EuMemberStates.cs',
)

/**
 * Reads the two-letter codes written in the backend list.
 *
 * @returns The codes in the order the backend file lists them.
 */
const backendCodes = (): string[] =>
  // The pattern has exactly one group, so every match carries it.
  [...readFileSync(BACKEND_LIST, 'utf8').matchAll(/"([A-Z]{2})"/g)].map((match) => match[1]!)

describe('EU_MEMBER_STATES', () => {
  it('holds the 27 member states once each', () => {
    expect(EU_MEMBER_STATES).toHaveLength(27)
    expect(new Set(EU_MEMBER_STATES).size).toBe(27)
  })

  it('uses GR for Greece, as client records store it', () => {
    expect(EU_MEMBER_STATES).toContain('GR')
    expect(EU_MEMBER_STATES).not.toContain('EL')
  })

  it('is the same set the API accepts', () => {
    expect([...EU_MEMBER_STATES].sort()).toEqual(backendCodes().sort())
  })
})
