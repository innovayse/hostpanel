/**
 * The words of a notification-feed event, written in the operator's language.
 *
 * The API sends the kind of an event and the facts about it (client, amount, subject); this is the one
 * place that turns them into a title, a detail line and a time, so the drawer and the full page read alike.
 */

import { useI18n } from 'vue-i18n'
import { formatMonthDay, formatRelativeFromNow, toIsoDay } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import { ActivityEventType } from '@/types/activityevent'
import type { ActivityEvent } from '@/types/activityevent'

/** Separates the client name from the amount on the detail line. */
const DETAIL_SEPARATOR = ' · '

/**
 * Words for notification-feed events in the active locale.
 *
 * @returns `title`, `detail` and `time` for one event.
 */
export const useActivityEventText = () => {
  const { t, locale } = useI18n()

  /**
   * The headline of an event, from its type and facts.
   *
   * @param event - The feed event.
   * @returns The localised title ("Invoice #33 overdue").
   */
  const title = (event: ActivityEvent): string => {
    switch (event.type) {
      case ActivityEventType.ClientRegistered:
        return t('notifications.type.ClientRegistered')
      case ActivityEventType.InvoiceOverdue:
        return t('notifications.type.InvoiceOverdue', { number: event.entityId })
      case ActivityEventType.PaymentReceived:
        return t('notifications.type.PaymentReceived', { number: event.entityId })
      case ActivityEventType.TicketOpened:
        return t('notifications.type.TicketOpened', { subject: event.subject ?? '' })
      case ActivityEventType.DomainExpiring:
        return t('notifications.type.DomainExpiring', { domain: event.subject ?? '' })
    }
  }

  /**
   * The second line of an event: the client and, when the event has one, the amount.
   *
   * @param event - The feed event.
   * @returns The client name, then the amount, joined by a separator; empty when neither is known.
   */
  const detail = (event: ActivityEvent): string => {
    const amount = event.amount !== null && event.currency !== null ? formatPrice(event.amount, event.currency) : ''
    return [event.clientName, amount].filter((part) => part !== '').join(DETAIL_SEPARATOR)
  }

  /**
   * When an event happened: how long ago it was, or the date when it is still to come.
   *
   * @param event - The feed event.
   * @param now - Current time in epoch milliseconds (injectable for tests).
   * @returns Relative wording from `Intl.RelativeTimeFormat` in the active locale, or "On <date>".
   */
  const time = (event: ActivityEvent, now: number = Date.now()): string => {
    if (new Date(event.occurredAt).getTime() > now) {
      return t('notifications.upcoming', { date: formatMonthDay(toIsoDay(new Date(event.occurredAt)), locale.value) })
    }
    return formatRelativeFromNow(event.occurredAt, locale.value, now)
  }

  return { title, detail, time }
}
