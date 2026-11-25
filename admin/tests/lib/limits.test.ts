/**
 * Unit tests for the shared limits: time units agree with each other, the notice and debounce
 * timings are positive, and the page sizes keep the values the lists were written against.
 */
import { PollKind } from '@/types/poller'
import { describe, expect, it } from 'vitest'
import {
  BILLABLE_ITEM_DESCRIPTION_MAX_LENGTH,
  BYTES_PER_KB,
  COPIED_VISIBLE_MS,
  DEFAULT_PAGE_SIZE,
  LEDGER_PAGE_SIZE,
  LOOKUP_PAGE_SIZE,
  MS_PER_DAY,
  MS_PER_MINUTE,
  MS_PER_SECOND,
  NOTICE_VISIBLE_MS,
  POLL_INTERVALS_MS,
  REPORT_PAGE_SIZE,
  SEARCH_DEBOUNCE_MS,
  SHORT_TEXT_MAX_LENGTH,
  CURRENCY_CODE_LENGTH,
  MAX_PERCENT,
  TWO_FACTOR_CODE_LENGTH,
} from '@/lib/limits'

describe('limits', () => {
  it('builds each time unit from the one below it', () => {
    expect(MS_PER_SECOND).toBe(1000)
    expect(MS_PER_MINUTE).toBe(60_000)
    expect(MS_PER_DAY).toBe(86_400_000)
  })

  it('keeps the notice and debounce timings the screens were written against', () => {
    expect(NOTICE_VISIBLE_MS).toBe(3000)
    expect(SEARCH_DEBOUNCE_MS).toBe(300)
  })

  it('caps a billable item description where the API does', () => {
    expect(BILLABLE_ITEM_DESCRIPTION_MAX_LENGTH).toBe(500)
  })

  it('keeps the page sizes of each kind of list', () => {
    expect([DEFAULT_PAGE_SIZE, LEDGER_PAGE_SIZE, REPORT_PAGE_SIZE, LOOKUP_PAGE_SIZE]).toEqual([20, 25, 50, 100])
  })

  it('keeps the field and unit sizes', () => {
    expect(BYTES_PER_KB).toBe(1024)
    expect(SHORT_TEXT_MAX_LENGTH).toBe(200)
    expect(CURRENCY_CODE_LENGTH).toBe(3)
    expect(TWO_FACTOR_CODE_LENGTH).toBe(6)
    expect(MAX_PERCENT).toBe(100)
  })

  it('keeps the interval of every kind of poller', () => {
    expect(POLL_INTERVALS_MS).toEqual({
      [PollKind.MigrationStatus]: 2000,
      [PollKind.ServerRestart]: 2000,
      [PollKind.PluginFollowUp]: 3000,
      [PollKind.DemoCountdown]: 60000,
    })
  })

  it('keeps the copied-notice timing', () => {
    expect(COPIED_VISIBLE_MS).toBe(2000)
  })
})
