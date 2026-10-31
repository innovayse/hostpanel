/**
 * Factory for the signed-in account in the shape `GET /api/portal/client/me` answers with (the
 * backend `MyProfileDto` plus permissions), so tests build the real wire object instead of each
 * spelling out every field.
 *
 * @module tests/helpers/clientUser
 */

import type { ClientUser } from '~/types/clientuser'

/**
 * Builds an account with a client record, no address, no optional details and no permissions, acting for its account (so the missing permissions bite).
 *
 * @param overrides - Fields to set on top of the defaults.
 * @returns The account.
 */
export const makeClientUser = (overrides: Partial<ClientUser> = {}): ClientUser => ({
  id: 1,
  email: 'ada@example.test',
  firstName: 'Ada',
  lastName: 'Lovelace',
  companyName: null,
  phone: null,
  address: null,
  language: null,
  currency: null,
  paymentMethod: null,
  notifications: null,
  twoFactorEnabled: false,
  contacts: [],
  hasClientProfile: true,
  permissions: 0,
  actsForAccount: true,
  ...overrides
})
