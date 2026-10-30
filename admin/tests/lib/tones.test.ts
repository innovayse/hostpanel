/**
 * Tests for the tone module: every known value of each closed set maps to its tone, an unknown
 * one falls back to neutral, every member of an enum-keyed table is covered, and `statusLabel`
 * prefers the translated option over the raw value.
 */
import { describe, expect, it } from 'vitest'
import {
  cancellationStatusLabel,
  cancellationStatusTone,
  cancellationTypeLabel,
  cancellationTypeTone,
  clientStatusTone,
  contactTypeTone,
  domainStatusTone,
  gatewayLogResultTone,
  invoiceStatusTone,
  migrationActionTone,
  migrationStatusTone,
  networkIssuePriorityTone,
  networkIssueStatusTone,
  orderItemStatusTone,
  orderStatusTone,
  quoteStageTone,
  reportStatusTone,
  serviceStatusTone,
  statusLabel,
  ticketPriorityTone,
  ticketStatusTone,
  transactionTypeTone,
} from '@/lib/tones'
import { ClientStatus } from '@/types/client'
import { ContactType } from '@/types/contact'
import { DomainStatus } from '@/types/domain'
import { GatewayLogResult } from '@/types/gatewaylog'
import { InvoiceStatus, InvoiceTransactionType } from '@/types/invoice'
import { MigrationLogAction, MigrationStatus } from '@/types/migration'
import { NetworkIssuePriority, NetworkIssueStatus } from '@/types/networkissue'
import { OrderItemStatus, OrderStatus } from '@/types/order'
import { QuoteStage } from '@/types/quote'
import { CancellationStatus, CancellationType, ServiceStatus } from '@/types/service'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import { UiTone } from '@/utils/ui/tone'

/**
 * A wire value a newer API could send that this build's enum does not know yet. The cast is the
 * point: the functions are typed to the enum, and the test proves what they do at runtime anyway.
 *
 * @param value - The unknown wire value.
 * @returns The value typed as the enum.
 */
const fromNewerApi = <T extends string>(value: string): T => value as T

describe('lifecycle status tones', () => {
  it('colours each order status', () => {
    expect(orderStatusTone(OrderStatus.Pending)).toBe(UiTone.Warning)
    expect(orderStatusTone(OrderStatus.Active)).toBe(UiTone.Success)
    expect(orderStatusTone(OrderStatus.Cancelled)).toBe(UiTone.Danger)
    expect(orderStatusTone(OrderStatus.Fraud)).toBe(UiTone.Danger)
  })

  it('colours each order item status', () => {
    expect(orderItemStatusTone(OrderItemStatus.Pending)).toBe(UiTone.Warning)
    expect(orderItemStatusTone(OrderItemStatus.Active)).toBe(UiTone.Success)
    expect(orderItemStatusTone(OrderItemStatus.Cancelled)).toBe(UiTone.Danger)
  })

  it('colours each service status', () => {
    expect(serviceStatusTone(ServiceStatus.Active)).toBe(UiTone.Success)
    expect(serviceStatusTone(ServiceStatus.Pending)).toBe(UiTone.Primary)
    expect(serviceStatusTone(ServiceStatus.Suspended)).toBe(UiTone.Warning)
    expect(serviceStatusTone(ServiceStatus.Terminated)).toBe(UiTone.Neutral)
  })

  it('colours each domain status', () => {
    expect(domainStatusTone(DomainStatus.Active)).toBe(UiTone.Success)
    expect(domainStatusTone(DomainStatus.PendingRegistration)).toBe(UiTone.Primary)
    expect(domainStatusTone(DomainStatus.PendingTransfer)).toBe(UiTone.Primary)
    expect(domainStatusTone(DomainStatus.Expired)).toBe(UiTone.Danger)
    expect(domainStatusTone(DomainStatus.Redemption)).toBe(UiTone.Warning)
    expect(domainStatusTone(DomainStatus.Transferred)).toBe(UiTone.Neutral)
    expect(domainStatusTone(DomainStatus.Cancelled)).toBe(UiTone.Neutral)
  })

  it('colours each invoice status', () => {
    expect(invoiceStatusTone(InvoiceStatus.Paid)).toBe(UiTone.Success)
    expect(invoiceStatusTone(InvoiceStatus.Unpaid)).toBe(UiTone.Warning)
    expect(invoiceStatusTone(InvoiceStatus.Overdue)).toBe(UiTone.Danger)
    expect(invoiceStatusTone(InvoiceStatus.Refunded)).toBe(UiTone.Info)
    expect(invoiceStatusTone(InvoiceStatus.PartiallyRefunded)).toBe(UiTone.Info)
    expect(invoiceStatusTone(InvoiceStatus.Draft)).toBe(UiTone.Primary)
    expect(invoiceStatusTone(InvoiceStatus.Cancelled)).toBe(UiTone.Neutral)
  })

  it('keeps the enum values equal to the wire strings', () => {
    expect(OrderStatus.Fraud).toBe('Fraud')
    expect(DomainStatus.PendingRegistration).toBe('PendingRegistration')
  })
})

describe('client tones', () => {
  it('maps each client status to a tone', () => {
    expect(clientStatusTone(ClientStatus.Active)).toBe(UiTone.Success)
    expect(clientStatusTone(ClientStatus.Suspended)).toBe(UiTone.Warning)
    expect(clientStatusTone(ClientStatus.Closed)).toBe(UiTone.Danger)
    expect(clientStatusTone(ClientStatus.Inactive)).toBe(UiTone.Neutral)
  })

  it('maps each contact type to a tone', () => {
    expect(contactTypeTone(ContactType.Billing)).toBe(UiTone.Primary)
    expect(contactTypeTone(ContactType.Technical)).toBe(UiTone.Warning)
    expect(contactTypeTone(ContactType.General)).toBe(UiTone.Neutral)
  })
})

describe('billing tones', () => {
  it('maps quote stages to tones', () => {
    expect(quoteStageTone(QuoteStage.Accepted)).toBe(UiTone.Success)
    expect(quoteStageTone(QuoteStage.Lost)).toBe(UiTone.Danger)
    expect(quoteStageTone(QuoteStage.OnHold)).toBe(UiTone.Warning)
  })

  it('maps transaction types to tones', () => {
    expect(transactionTypeTone(InvoiceTransactionType.Payment)).toBe(UiTone.Success)
    expect(transactionTypeTone(InvoiceTransactionType.Refund)).toBe(UiTone.Danger)
  })

  it('maps gateway log outcomes to tones', () => {
    expect(gatewayLogResultTone(GatewayLogResult.Payment)).toBe(UiTone.Success)
    expect(gatewayLogResultTone(GatewayLogResult.Refund)).toBe(UiTone.Danger)
    expect(gatewayLogResultTone(GatewayLogResult.Credit)).toBe(UiTone.Warning)
    expect(gatewayLogResultTone(GatewayLogResult.Pending)).toBe(UiTone.Info)
  })
})

describe('report status tones', () => {
  it('maps good, waiting, bad and closed statuses to their tones', () => {
    expect(reportStatusTone('Active')).toBe(UiTone.Success)
    expect(reportStatusTone('Pending')).toBe(UiTone.Warning)
    expect(reportStatusTone('Overdue')).toBe(UiTone.Danger)
    expect(reportStatusTone('Cancelled')).toBe(UiTone.Neutral)
    expect(reportStatusTone('Refunded')).toBe(UiTone.Info)
    expect(reportStatusTone('PartiallyRefunded')).toBe(UiTone.Info)
  })
})

describe('support tones', () => {
  it('covers every ticket status and priority', () => {
    for (const status of Object.values(TicketStatus)) {
      expect(ticketStatusTone[status]).toBeDefined()
    }
    for (const priority of Object.values(TicketPriority)) {
      expect(ticketPriorityTone[priority]).toBeDefined()
    }
  })

  it('covers every network issue priority and status', () => {
    for (const priority of Object.values(NetworkIssuePriority)) {
      expect(networkIssuePriorityTone[priority]).toBeDefined()
    }
    for (const status of Object.values(NetworkIssueStatus)) {
      expect(networkIssueStatusTone[status]).toBeDefined()
    }
  })

  it('marks the extremes: closed is neutral, high and critical stand out', () => {
    expect(ticketStatusTone[TicketStatus.Closed]).toBe(UiTone.Neutral)
    expect(ticketPriorityTone[TicketPriority.High]).toBe(UiTone.Danger)
    expect(networkIssuePriorityTone[NetworkIssuePriority.Critical]).toBe(UiTone.Danger)
    expect(networkIssueStatusTone[NetworkIssueStatus.Resolved]).toBe(UiTone.Success)
  })
})

describe('migration tones', () => {
  it('gives every job state and log outcome a tone', () => {
    for (const status of Object.values(MigrationStatus)) {
      expect(migrationStatusTone[status]).toBeTruthy()
    }
    for (const action of Object.values(MigrationLogAction)) {
      expect(migrationActionTone[action]).toBeTruthy()
    }
  })

  it('colours failure as danger and success as success', () => {
    expect(migrationStatusTone[MigrationStatus.Failed]).toBe(UiTone.Danger)
    expect(migrationStatusTone[MigrationStatus.Completed]).toBe(UiTone.Success)
    expect(migrationActionTone[MigrationLogAction.Skipped]).toBe(UiTone.Warning)
  })
})

describe('unknown values', () => {
  it('fall back to neutral for a value the API added later', () => {
    expect(orderStatusTone(fromNewerApi<OrderStatus>('Archived'))).toBe(UiTone.Neutral)
    expect(domainStatusTone('')).toBe(UiTone.Neutral)
    expect(clientStatusTone('Archived')).toBe(UiTone.Neutral)
    expect(contactTypeTone('Legal')).toBe(UiTone.Neutral)
    expect(quoteStageTone('Mystery')).toBe(UiTone.Neutral)
    expect(transactionTypeTone('Mystery')).toBe(UiTone.Neutral)
    expect(gatewayLogResultTone('Mystery')).toBe(UiTone.Neutral)
    expect(reportStatusTone('Something else')).toBe(UiTone.Neutral)
  })
})

describe('statusLabel', () => {
  const options = [{ value: 'Active', label: 'Aktiv' }]

  it('returns the translated label of a known status', () => {
    expect(statusLabel(options, 'Active')).toBe('Aktiv')
  })

  it('returns the raw status when the list does not know it', () => {
    expect(statusLabel(options, 'Archived')).toBe('Archived')
  })
})

describe('cancellation requests', () => {
  it('tones an immediate cancellation red and an open request green', () => {
    expect(cancellationTypeTone(CancellationType.Immediate)).toBe(UiTone.Danger)
    expect(cancellationTypeTone(CancellationType.EndOfBillingPeriod)).toBe(UiTone.Primary)
    expect(cancellationStatusTone(CancellationStatus.Open)).toBe(UiTone.Success)
    expect(cancellationStatusTone(CancellationStatus.Closed)).toBe(UiTone.Neutral)
    expect(cancellationTypeTone(fromNewerApi<CancellationType>('Mystery'))).toBe(UiTone.Neutral)
  })

  it('words the known types and statuses and shows an unknown one as received', () => {
    expect(cancellationTypeLabel(CancellationType.Immediate)).toBe('Immediate')
    expect(cancellationTypeLabel(CancellationType.EndOfBillingPeriod)).toBe('End of Billing Period')
    expect(cancellationTypeLabel(fromNewerApi<CancellationType>('Mystery'))).toBe('Mystery')
    expect(cancellationStatusLabel(CancellationStatus.Open)).toBe('Open')
    expect(cancellationStatusLabel(CancellationStatus.Closed)).toBe('Closed')
    expect(cancellationStatusLabel(fromNewerApi<CancellationStatus>('Mystery'))).toBe('Mystery')
  })
})
