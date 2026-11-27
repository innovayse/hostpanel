/**
 * Tests for the extra domain field helpers: the kind of registrant and which values are sent. Every key is invented.
 */
import { describe, expect, it } from 'vitest'
import { attributesFor, holderOf } from '@/lib/domainFields'
import { type DomainField, DomainFieldHolder, DomainFieldType } from '@/types/domain'

/**
 * Builds a field.
 *
 * @param key - The field key.
 * @returns The field.
 */
const field = (key: string): DomainField => ({
  key,
  label: key,
  type: DomainFieldType.Text,
  required: false,
  description: null,
  placeholder: null,
  pattern: null,
  minLength: null,
  maxLength: null,
  options: [],
})

describe('holderOf', () => {
  it('is an organisation only when the contact names one', () => {
    expect(holderOf('Analytical Engines Ltd')).toBe(DomainFieldHolder.Organization)
    expect(holderOf('')).toBe(DomainFieldHolder.Individual)
    expect(holderOf('   ')).toBe(DomainFieldHolder.Individual)
    expect(holderOf(null)).toBe(DomainFieldHolder.Individual)
  })
})

describe('attributesFor', () => {
  it('keeps only the values of the fields that apply and drops the blank ones', () => {
    expect(attributesFor([field('a'), field('b')], { a: 'x', b: '  ', stale: 'y' })).toEqual({ a: 'x' })
  })
})
