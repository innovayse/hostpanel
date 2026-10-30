/**
 * One complete `ClientService` for the client-area tests, shaped exactly like the backend's
 * `ClientServiceDto`, so a test only overrides the fields it is about.
 *
 * @module tests/helpers/clientServiceFixture
 */

import type { ClientService } from '~/types/clientservice'
import { BillingCycle } from '~/types/billingcycle'
import { ServiceStatus } from '~/types/servicestatus'

/**
 * Builds an active, set-up service on a known server.
 *
 * @param overrides - Fields to set.
 * @returns A service.
 */
export const makeClientService = (overrides: Partial<ClientService> = {}): ClientService => ({
  id: 4,
  productId: 2,
  productName: 'Starter',
  isDigital: false,
  billingCycle: BillingCycle.Monthly,
  status: ServiceStatus.Active,
  nextRenewalAt: '2026-12-01T00:00:00Z',
  domain: 'example.com',
  username: 'webmstr',
  price: 10,
  firstPaymentAmount: 12,
  paymentMethod: 'stripe',
  server: { name: 'srv1', hostname: 'srv1.example.net', ipAddress: '10.0.0.1' },
  createdAt: '2026-01-01T00:00:00Z',
  suspensionSource: null,
  usage: null,
  ...overrides,
})
