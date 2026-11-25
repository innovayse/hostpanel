/**
 * Pure helpers of the admin notification feed: the day groups the drawer draws and the filter that narrows it.
 *
 * Whether an event is read is the server's to say (`ActivityEvent.isRead`, kept per admin), so nothing here
 * remembers anything.
 */

import type { ActivityEvent } from '@/types/activityevent'

/** The day groups the drawer sorts events into, in the order they are drawn. */
export const ActivityGroup = {
  /** Events dated after today (a domain that is about to expire). */
  Upcoming: 'upcoming',
  /** Events dated today. */
  Today: 'today',
  /** Events dated before today. */
  Earlier: 'earlier',
} as const

/** One of the {@link ActivityGroup} values. */
export type ActivityGroup = (typeof ActivityGroup)[keyof typeof ActivityGroup]

/** One group of events under its heading. */
export interface ActivityGroupBucket {
  /** Which day group this is. */
  group: ActivityGroup
  /** The events of the group, in the order the feed delivered them. */
  events: ActivityEvent[]
}

/** Which events the drawer lists: all of the feed, or only the ones not read yet. */
export const ActivityFilter = {
  /** Every event of the feed. */
  All: 'all',
  /** Only the events not read yet. */
  Unread: 'unread',
} as const

/** One of the {@link ActivityFilter} values. */
export type ActivityFilter = (typeof ActivityFilter)[keyof typeof ActivityFilter]

/**
 * Sorts events into day groups, dropping the groups that hold nothing.
 *
 * @param events - The feed events, newest first.
 * @param now - The current moment; the calendar day is the viewer's own (injectable for tests).
 * @returns The non-empty groups in the order Upcoming, Today, Earlier, each keeping the feed's order.
 */
export const groupActivityEvents = (events: ActivityEvent[], now: Date = new Date()): ActivityGroupBucket[] => {
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const startOfTomorrow = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1).getTime()
  const buckets: ActivityGroupBucket[] = [
    { group: ActivityGroup.Upcoming, events: [] },
    { group: ActivityGroup.Today, events: [] },
    { group: ActivityGroup.Earlier, events: [] },
  ]
  for (const event of events) {
    const at = new Date(event.occurredAt).getTime()
    if (at >= startOfTomorrow) {
      buckets[0].events.push(event)
    } else if (at >= startOfToday) {
      buckets[1].events.push(event)
    } else {
      buckets[2].events.push(event)
    }
  }
  return buckets.filter((bucket) => bucket.events.length > 0)
}
