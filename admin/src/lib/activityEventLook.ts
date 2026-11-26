/**
 * How each notification-feed event type looks: its icon and its tone.
 *
 * Shared by the notification drawer and the full notification page so the two read the same. The
 * tone comes from the shared vocabulary (`utils/ui/tone`), so a screen takes the tinted circle or the
 * plain icon colour from it and both follow the theme.
 */

import { Icon } from '@/lib/icons'
import { ActivityEventType } from '@/types/activityevent'
import { UiTone } from '@/utils/ui/tone'

/** Icon markup and tone of one event type. */
export interface ActivityEventLook {
  /** Inner SVG markup of the icon. */
  icon: string
  /** Semantic tone of the icon and of the circle that frames it. */
  tone: UiTone
}

/** Icon and tone per event type, so a list reads at a glance: overdue and expiring warn, a payment succeeds, a ticket informs. */
export const ACTIVITY_EVENT_LOOK: Record<ActivityEventType, ActivityEventLook> = {
  [ActivityEventType.ClientRegistered]: { icon: Icon.Users, tone: UiTone.Primary },
  [ActivityEventType.InvoiceOverdue]: { icon: Icon.Alert, tone: UiTone.Warning },
  [ActivityEventType.DomainExpiring]: { icon: Icon.Globe, tone: UiTone.Warning },
  [ActivityEventType.PaymentReceived]: { icon: Icon.Dollar, tone: UiTone.Success },
  [ActivityEventType.TicketOpened]: { icon: Icon.Chat, tone: UiTone.Info },
}
