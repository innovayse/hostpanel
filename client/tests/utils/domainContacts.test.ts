/**
 * Tests for `utils/domainContacts.ts`: the field renaming and the dropdown choices.
 */
import { describe, expect, it } from 'vitest'
import type { AccountContact } from '~/types/accountcontact'
import { ContactType } from '~/types/contacttype'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { contactFromProfile, contactFromRegistrant, emptyContact, toContactChoices } from '~/utils/domainContacts'

/**
 * Builds a contact with every optional field unset.
 *
 * @param overrides - Fields to set.
 * @returns The contact.
 */
const contactWith = (overrides: Partial<AccountContact>): AccountContact => ({
  id: 1,
  firstName: '',
  lastName: '',
  companyName: null,
  email: '',
  phone: null,
  type: ContactType.General,
  address: { street: null, address2: null, city: null, state: null, postCode: null, country: null },
  notifications: { general: true, invoice: true, support: true, product: true, domain: false, affiliate: false },
  ...overrides
})

describe('emptyContact', () => {
  it('has every field empty and is a new object each time', () => {
    const first = emptyContact()

    expect(Object.values(first).every((value) => value === '')).toBe(true)
    expect(emptyContact()).not.toBe(first)
  })
})

describe('contactFromProfile', () => {
  it('renames the profile fields to the WHOIS fields', () => {
    const contact = contactFromProfile(makeClientUser({
      firstName: 'Ada', lastName: 'Lovelace', companyName: 'Engines', email: 'ada@example.com', phone: '+44 1',
      address: { street: '1 Main St', address2: 'Flat 2', city: 'London', state: 'LDN', postCode: 'N1', country: 'GB' }
    }))

    expect(contact).toEqual({
      firstName: 'Ada', lastName: 'Lovelace', organization: 'Engines', email: 'ada@example.com',
      address1: '1 Main St', address2: 'Flat 2', city: 'London', state: 'LDN',
      postalCode: 'N1', country: 'GB', phone: '+44 1'
    })
  })

  it('reads null fields and a missing address as empty', () => {
    const profile = makeClientUser({ firstName: 'Ada', lastName: '', email: '' })

    expect(contactFromProfile(profile)).toEqual({ ...emptyContact(), firstName: 'Ada' })
  })
})

describe('contactFromRegistrant', () => {
  it('maps the registrar contact and reads the null optional lines as empty', () => {
    expect(contactFromRegistrant({
      firstName: 'Ada', lastName: 'Lovelace', organization: null, email: 'ada@example.com', phone: '+44 1',
      address1: '1 Main St', address2: null, city: 'London', state: 'LDN', postalCode: 'N1', country: 'GB',
      extendedAttributes: {}
    })).toEqual({
      firstName: 'Ada', lastName: 'Lovelace', organization: '', email: 'ada@example.com',
      address1: '1 Main St', address2: '', city: 'London', state: 'LDN',
      postalCode: 'N1', country: 'GB', phone: '+44 1'
    })
  })
})

describe('toContactChoices', () => {
  it('labels a choice with the full name and pre-fills every WHOIS field the contact carries', () => {
    const [choice] = toContactChoices([contactWith({
      id: 7, firstName: 'Ada', lastName: 'Lovelace', companyName: 'Engines', email: 'ada@example.com', phone: '+44 1',
      address: { street: '1 Main St', address2: null, city: 'London', state: null, postCode: 'N1', country: 'GB' }
    })])

    expect(choice).toEqual({
      id: '7',
      label: 'Ada Lovelace',
      fields: {
        firstName: 'Ada', lastName: 'Lovelace', organization: 'Engines', email: 'ada@example.com',
        address1: '1 Main St', address2: '', city: 'London', state: '', postalCode: 'N1', country: 'GB', phone: '+44 1'
      }
    })
  })

  it('trims the label of a contact with one name', () => {
    expect(toContactChoices([contactWith({ firstName: 'Ada', lastName: '' })])[0]!.label).toBe('Ada')
  })
})
