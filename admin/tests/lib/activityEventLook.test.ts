/**
 * Tests for the notification-feed event look: every event type has an icon and a theme-driven tone.
 */
import { describe, expect, it } from 'vitest'
import { ACTIVITY_EVENT_LOOK } from '@/lib/activityEventLook'
import { ActivityEventType } from '@/types/activityevent'
import { UiTone } from '@/utils/ui/tone'

describe('ACTIVITY_EVENT_LOOK', () => {
  it('covers every event type with an icon', () => {
    for (const type of Object.values(ActivityEventType)) {
      expect(ACTIVITY_EVENT_LOOK[type].icon).not.toBe('')
    }
  })

  it('takes the tones from the shared vocabulary: overdue warns, a payment succeeds, a ticket informs', () => {
    expect(ACTIVITY_EVENT_LOOK[ActivityEventType.InvoiceOverdue].tone).toBe(UiTone.Warning)
    expect(ACTIVITY_EVENT_LOOK[ActivityEventType.PaymentReceived].tone).toBe(UiTone.Success)
    expect(ACTIVITY_EVENT_LOOK[ActivityEventType.TicketOpened].tone).toBe(UiTone.Info)
    expect(ACTIVITY_EVENT_LOOK[ActivityEventType.ClientRegistered].tone).toBe(UiTone.Primary)
    expect(ACTIVITY_EVENT_LOOK[ActivityEventType.DomainExpiring].tone).toBe(UiTone.Warning)
  })
})
