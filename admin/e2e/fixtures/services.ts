/**
 * Fixtures for the client service detail page: the hosting service of the first client as
 * `GET /api/services/{id}` returns it, live and terminated.
 */
import { BillingCycle, ServiceStatus, type ServiceDetail } from '../../src/types/service'
import { hostingProductName, hostingService } from './orders'

/** The hosting service while it runs, so every section except the termination date is offered. */
export const activeServiceDetail: ServiceDetail = {
  id: hostingService.id,
  clientId: hostingService.clientId,
  clientName: hostingService.clientName,
  productId: 3,
  productName: hostingProductName,
  domain: 'engines.example.test',
  status: ServiceStatus.Active,
  allowedStatuses: [ServiceStatus.Active, ServiceStatus.Suspended, ServiceStatus.Terminated],
  createdAt: '2026-10-01T00:00:00Z',
  terminatedAt: null,
  adminNotes: 'Moved from the old server.',
  serverId: null,
  server: null,
  account: { dedicatedIp: null, username: 'ada', password: null, provisioningRef: null },
  billing: {
    quantity: 1,
    firstPaymentAmount: 9.99,
    recurringAmount: 9.99,
    paymentMethod: null,
    promotionCode: null,
    subscriptionId: null,
    billingCycle: BillingCycle.Monthly,
    nextDueDate: '2026-12-01T00:00:00Z',
    autoRenew: true,
  },
  automation: { overrideAutoSuspend: false, suspendUntil: null, autoTerminateEndOfCycle: false, autoTerminateReason: null },
}

/** The same service after it was terminated, so the termination date can be corrected. */
export const terminatedServiceDetail: ServiceDetail = {
  ...activeServiceDetail,
  status: ServiceStatus.Terminated,
  allowedStatuses: [ServiceStatus.Terminated],
  terminatedAt: '2026-10-20T00:00:00Z',
}
