/**
 * Tests for the notification link: each event kind points at the screen of the record it is about.
 */
import { describe, expect, it } from 'vitest'
import { activityEventLink } from '@/lib/activityEventLink'
import { ActivityEventType } from '@/types/activityevent'
import { activityEvent } from '../helpers/activityEvents'

describe('activityEventLink', () => {
  it('opens the new client', () => {
    expect(activityEventLink(activityEvent({ type: ActivityEventType.ClientRegistered, entityId: 12 }))).toBe('/clients/12')
  })

  it('opens the overdue invoice', () => {
    expect(activityEventLink(activityEvent({ type: ActivityEventType.InvoiceOverdue, entityId: 12 }))).toBe('/billing/12')
  })

  it('opens the invoice a payment was made for', () => {
    expect(activityEventLink(activityEvent({ type: ActivityEventType.PaymentReceived, entityId: 12 }))).toBe('/billing/12')
  })

  it('opens the ticket', () => {
    expect(activityEventLink(activityEvent({ type: ActivityEventType.TicketOpened, entityId: 12 }))).toBe('/support/tickets/12')
  })

  it('opens the domains list for an expiring domain', () => {
    expect(activityEventLink(activityEvent({ type: ActivityEventType.DomainExpiring, entityId: 12 }))).toBe('/domains')
  })
})
