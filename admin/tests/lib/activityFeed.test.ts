/**
 * Tests for the notification-feed helpers: the day groups.
 */
import { describe, expect, it } from 'vitest'
import { ActivityGroup, groupActivityEvents } from '@/lib/activityFeed'
import { activityEvent } from '../helpers/activityEvents'

/** A fixed local noon, so the day boundaries are the same wherever the suite runs. */
const NOW = new Date(2026, 9, 14, 12, 0, 0)

/**
 * Builds the ISO timestamp of a local moment.
 *
 * @param day - Day of October 2026.
 * @param hour - Hour of that day.
 * @returns ISO 8601 timestamp.
 */
const at = (day: number, hour: number): string => new Date(2026, 9, day, hour, 0, 0).toISOString()

describe('groupActivityEvents', () => {
  it('sorts events into Upcoming, Today and Earlier and keeps the feed order inside each', () => {
    const upcoming = activityEvent({ entityId: 1, occurredAt: at(20, 9) })
    const laterToday = activityEvent({ entityId: 2, occurredAt: at(14, 23) })
    const earlierToday = activityEvent({ entityId: 3, occurredAt: at(14, 1) })
    const yesterday = activityEvent({ entityId: 4, occurredAt: at(13, 23) })
    const lastWeek = activityEvent({ entityId: 5, occurredAt: at(7, 9) })

    const buckets = groupActivityEvents([upcoming, laterToday, earlierToday, yesterday, lastWeek], NOW)

    expect(buckets.map((bucket) => bucket.group)).toEqual([ActivityGroup.Upcoming, ActivityGroup.Today, ActivityGroup.Earlier])
    expect(buckets[0]?.events).toEqual([upcoming])
    expect(buckets[1]?.events).toEqual([laterToday, earlierToday])
    expect(buckets[2]?.events).toEqual([yesterday, lastWeek])
  })

  it('drops the groups that hold nothing', () => {
    const buckets = groupActivityEvents([activityEvent({ occurredAt: at(1, 9) })], NOW)

    expect(buckets.map((bucket) => bucket.group)).toEqual([ActivityGroup.Earlier])
    expect(groupActivityEvents([], NOW)).toEqual([])
  })
})
