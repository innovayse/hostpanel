/**
 * Tests that the closed value sets keep the exact strings the API sends, so renaming a member in
 * code can never silently change what goes over the wire.
 */
import { describe, expect, it } from 'vitest'
import { ActivityEventType } from '@/types/activityevent'
import { LogoutScope } from '@/types/auth'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import { ClientStatus } from '@/types/client'
import { ContactType } from '@/types/contact'
import { CurrencyCode } from '@/types/currency'
import { DemoAccountRole } from '@/types/demo'
import { DnsRecordType, DomainStatus } from '@/types/domain'
import { PaymentGateway } from '@/types/gateway'
import { IntegrationFieldType } from '@/types/integration'
import { InvoiceOpenAction, InvoiceStatus } from '@/types/invoice'
import { MigrationEntityType } from '@/types/migration'
import { OrderItemStatus, OrderStatus } from '@/types/order'
import { BillingCycle, ServiceStatus } from '@/types/service'
import { ServerRefresh } from '@/types/server'
import { PaymentMethod } from '@/types/transaction'
import { BrandingImageKind } from '@/types/upload'

describe('status enums', () => {
  it('are spelled as the API spells them', () => {
    expect(Object.values(DemoAccountRole)).toEqual(['Admin', 'Client'])
    expect(Object.values(OrderStatus)).toEqual(['Pending', 'Active', 'Cancelled', 'Fraud'])
    expect(Object.values(OrderItemStatus)).toEqual(['Pending', 'Active', 'Cancelled'])
    expect(Object.values(ServiceStatus)).toEqual(['Active', 'Pending', 'Suspended', 'Terminated'])
    expect(Object.values(ClientStatus)).toEqual(['Active', 'Inactive', 'Suspended', 'Closed'])
    expect(Object.values(ContactType)).toEqual(['Billing', 'Technical', 'General'])
    expect(InvoiceStatus.PaymentPending).toBe('PaymentPending')
    expect(InvoiceStatus.PartiallyRefunded).toBe('PartiallyRefunded')
    expect(DomainStatus.PendingTransfer).toBe('PendingTransfer')
  })
})

describe('billing value sets', () => {
  it('keep the wire strings of methods, gateways, cycles and actions', () => {
    expect(PaymentMethod.Card).toBe('Credit/Debit Card')
    expect(PaymentMethod.BankTransfer).toBe('Bank Transfer')
    expect(PaymentGateway.BankTransfer).toBe('BankTransfer')
    expect(Object.values(BillingCycle)).toEqual(['Monthly', 'Annual'])
    expect(Object.values(BillableItemType)).toEqual(['OneTime', 'Recurring'])
    expect(Object.values(RecurringPeriod)).toEqual(['weekly', 'monthly', 'quarterly', 'annually'])
    expect(Object.values(DnsRecordType)).toEqual(['A', 'AAAA', 'CNAME', 'MX', 'TXT', 'NS', 'SRV'])
    expect(Object.values(CurrencyCode)).toEqual(['AMD', 'USD', 'EUR', 'GBP', 'RUB'])
    expect(Object.values(InvoiceOpenAction)).toEqual(['view', 'edit'])
  })
})

describe('screen value sets', () => {
  it('keep the strings the API and the routes use', () => {
    expect(Object.values(ActivityEventType)).toEqual(['ClientRegistered', 'InvoiceOverdue', 'DomainExpiring'])
    expect(Object.values(LogoutScope)).toEqual(['this', 'all'])
    expect(Object.values(IntegrationFieldType)).toEqual(['Text', 'Password', 'Secret', 'Select', 'TextArea', 'Toggle'])
    expect(Object.values(BrandingImageKind)).toEqual(['logo', 'logodark', 'logomark', 'favicon'])
    expect(Object.values(ServerRefresh)).toEqual(['servers', 'both'])
    expect(MigrationEntityType.TicketReplies).toBe('TicketReplies')
    expect(Object.values(MigrationEntityType)).toHaveLength(15)
  })
})
