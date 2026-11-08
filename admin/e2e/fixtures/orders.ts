/**
 * Fixtures for the orders, services and domain lists: one row of each as the list endpoints
 * return it.
 */
import { DomainStatus, type DomainRegistration } from '../../src/types/domain'
import type { OrderListItem } from '../../src/types/order'
import { OrderStatus } from '../../src/types/order'
import type { PaginatedList } from '../../src/types/paginatedlist'
import { BillingCycle, ServiceStatus, type ServiceListItem } from '../../src/types/service'

/** An order waiting for the operator, so accept and cancel are offered. */
export const pendingOrder: OrderListItem = {
  id: 31,
  orderNumber: 'ORD-0031',
  clientId: 7,
  clientName: 'Ada Lovelace',
  status: OrderStatus.Pending,
  paymentMethod: 'Example Gateway',
  total: 12.5,
  currency: 'USD',
  invoiceId: null,
  itemCount: 2,
  failedLineCount: 0,
  createdAt: '2026-10-10T10:00:00Z',
}

/** `GET /api/orders`: one page with the pending order. */
export const ordersPage: PaginatedList<OrderListItem> = { items: [pendingOrder], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }

/** Product name of the hosting service, kept non-null so tests can assert on it. */
export const hostingProductName = 'Shared Hosting'

/** A hosting service of the first client. */
export const hostingService: ServiceListItem = {
  id: 41,
  clientId: 7,
  clientName: 'Ada Lovelace',
  productName: hostingProductName,
  domain: 'engines.example.test',
  price: 9.99,
  priceCurrency: 'USD',
  billingCycle: BillingCycle.Monthly,
  status: ServiceStatus.Active,
  nextDueDate: '2026-12-01T00:00:00Z',
}

/** `GET /api/services`: one page with the hosting service. */
export const servicesPage: PaginatedList<ServiceListItem> = { items: [hostingService], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }

/** A registered domain of the second client. */
export const registeredDomain: DomainRegistration = {
  id: 51,
  clientId: 8,
  clientName: 'Grace Hopper',
  name: 'compiler.example.test',
  regPeriodYears: 1,
  registrar: null,
  recurringAmount: 12.99,
  priceCurrency: 'USD',
  nextDueDate: '2027-01-01T00:00:00Z',
  expiresAt: '2027-01-01T00:00:00Z',
  status: DomainStatus.Active,
  autoRenew: true,
}

/** `GET /api/domains`: one page with the domain. */
export const domainsPage: PaginatedList<DomainRegistration> = { items: [registeredDomain], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }
