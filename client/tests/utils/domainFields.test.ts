/**
 * Tests for `utils/domainFields.ts`: the kind of registrant, what is sent, what is missing and which refusal belongs to
 * which field. Every key is invented: the fields come from the registrar plugin, the helpers know none.
 *
 * @module tests/utils/domainFields.test
 */

import { describe, expect, it } from 'vitest'
import type { DomainField } from '~/types/domainfield'
import { DomainFieldHolder } from '~/types/domainfieldholder'
import { DomainFieldType } from '~/types/domainfieldtype'
import { attributesFor, compactAttributes, fieldErrorFor, holderOf, missingRequiredKeys } from '~/utils/domainFields'

/**
 * Builds a field.
 *
 * @param key - The field key.
 * @param required - Whether the field is required.
 * @returns The field.
 */
const field = (key: string, required = false): DomainField => ({
  key,
  label: key,
  type: DomainFieldType.Text,
  required,
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
    expect(holderOf(undefined)).toBe(DomainFieldHolder.Individual)
  })
})

describe('compactAttributes', () => {
  it('leaves out the blank values', () => {
    expect(compactAttributes({ first: 'one', second: '', third: '  ' })).toEqual({ first: 'one' })
  })
})

describe('missingRequiredKeys', () => {
  it('lists the required fields with no value, in the fields order', () => {
    const fields = [field('a', true), field('b'), field('c', true), field('d', true)]

    expect(missingRequiredKeys(fields, { a: 'x', b: '', c: '  ' })).toEqual(['c', 'd'])
  })
})

describe('attributesFor', () => {
  it('keeps only the values of the fields that apply and drops the blank ones', () => {
    expect(attributesFor([field('a'), field('b')], { a: 'x', b: '', stale: 'y' })).toEqual({ a: 'x' })
  })
})

describe('fieldErrorFor', () => {
  it('finds the refusal under the key as declared, then with each segment lower cased as the error reader writes it', () => {
    expect(fieldErrorFor({ 'holder-number': 'Bad.' }, 'holder-number')).toBe('Bad.')
    expect(fieldErrorFor({ 'holder.number': 'Bad.' }, 'Holder.Number')).toBe('Bad.')
    expect(fieldErrorFor({}, 'holder-number')).toBeUndefined()
  })
})
