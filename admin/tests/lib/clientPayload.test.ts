/**
 * Tests for `toUpdateClientPayload`: the update endpoint replaces the whole record, so the
 * payload built from a loaded client must carry every group, and a change only replaces its own.
 */

import { describe, expect, it } from 'vitest'
import { toUpdateClientPayload } from '@/lib/clientPayload'
import { ClientStatus, type ClientDetail } from '@/types/client'

/** A loaded client with every flag in a non-default position, so a dropped field shows. */
const CLIENT: ClientDetail = {
  id: 7,
  userId: 'user-7',
  email: 'ada@example.test',
  firstName: 'Ada',
  lastName: 'Lovelace',
  companyName: 'Analytical Engines',
  status: ClientStatus.Suspended,
  isUserDeleted: false,
  twoFactorEnabled: false,
  createdAt: '2026-01-01T00:00:00Z',
  phone: '+44 1',
  address: {
    street: '1 Main St',
    address2: 'Flat 2',
    city: 'London',
    state: 'LDN',
    postCode: 'N1',
    country: 'GB',
  },
  preferences: {
    language: 'en',
    currency: 'GBP',
    paymentMethod: 'card',
    billingContact: 'Accounts',
    adminNotes: 'old note',
  },
  notifications: {
    general: false,
    invoice: true,
    support: false,
    product: true,
    domain: false,
    affiliate: true,
  },
  settings: {
    overdueNotices: false,
    taxExempt: true,
    separateInvoices: true,
    disableCcProcessing: true,
    marketingOptIn: true,
    statusUpdate: false,
    allowSso: false,
  },
  contacts: [],
}

describe('toUpdateClientPayload', () => {
  it('carries every group of the loaded client, flags included', () => {
    const payload = toUpdateClientPayload(CLIENT, {})

    expect(payload).toMatchObject({
      firstName: 'Ada',
      lastName: 'Lovelace',
      companyName: 'Analytical Engines',
      phone: '+44 1',
      address: { street: '1 Main St', country: 'GB' },
      preferences: { paymentMethod: 'card', billingContact: 'Accounts', adminNotes: 'old note' },
      notifications: { general: false, invoice: true, support: false, product: true, domain: false, affiliate: true },
      settings: { taxExempt: true, allowSso: false, statusUpdate: false, overdueNotices: false },
    })
    // Every key of the payload is present, so none is stored as false or cleared by omission.
    expect(Object.keys(payload)).toHaveLength(11)
    expect(Object.keys(payload.notifications)).toHaveLength(6)
    expect(Object.keys(payload.settings)).toHaveLength(7)
  })

  it('leaves the email, language, currency and status for the API to keep', () => {
    const payload = toUpdateClientPayload(CLIENT, {})

    expect(payload.email).toBeNull()
    expect(payload.preferences.language).toBeNull()
    expect(payload.preferences.currency).toBeNull()
    expect(payload.status).toBeNull()
  })

  it('sends the loaded client group back so an unchanged edit does not drop it', () => {
    const grouped: ClientDetail = { ...CLIENT, clientGroupId: 3 }

    expect(toUpdateClientPayload(grouped, {}).clientGroupId).toBe(3)
    expect(toUpdateClientPayload(grouped, { firstName: 'Grace' }).clientGroupId).toBe(3)
  })

  it('sends null for a client without a group', () => {
    expect(toUpdateClientPayload(CLIENT, {}).clientGroupId).toBeNull()
    expect(toUpdateClientPayload({ ...CLIENT, clientGroupId: null }, {}).clientGroupId).toBeNull()
  })

  it('sends a chosen status as the enum member the API binds', () => {
    const payload = toUpdateClientPayload(CLIENT, { status: ClientStatus.Closed })

    expect(payload.status).toBe('Closed')
  })

  it('turns a field the client lacks into null instead of dropping it', () => {
    const payload = toUpdateClientPayload({ ...CLIENT, companyName: undefined, phone: undefined }, {})

    expect(payload.companyName).toBeNull()
    expect(payload.phone).toBeNull()
  })

  it('replaces only the group it is given', () => {
    const payload = toUpdateClientPayload(CLIENT, {
      preferences: { ...CLIENT.preferences, adminNotes: 'new note' },
    })

    expect(payload.preferences.adminNotes).toBe('new note')
    expect(payload.firstName).toBe('Ada')
    expect(payload.notifications.general).toBe(false)
  })
})
