/**
 * Tests for rendering the published time zone: an IANA id becomes its offset, free text stays as written.
 *
 * @module tests/utils/timezoneOffsetLabel.test
 */

import { describe, expect, it } from 'vitest'
import { timezoneOffsetLabel } from '~/utils/timezoneOffsetLabel'

/** A winter day, so a zone with daylight saving time is on its standard offset. */
const WINTER = new Date('2026-01-15T12:00:00Z')

/** A summer day, so a zone with daylight saving time is on its summer offset. */
const SUMMER = new Date('2026-07-15T12:00:00Z')

describe('timezoneOffsetLabel', () => {
  it('prints the offset of an IANA id', () => {
    expect(timezoneOffsetLabel('Asia/Yerevan', WINTER)).toBe('UTC+04:00')
  })

  it('follows daylight saving time', () => {
    expect(timezoneOffsetLabel('Europe/Berlin', WINTER)).toBe('UTC+01:00')
    expect(timezoneOffsetLabel('Europe/Berlin', SUMMER)).toBe('UTC+02:00')
  })

  it('prints a zero offset as UTC+00:00', () => {
    expect(timezoneOffsetLabel('UTC', WINTER)).toBe('UTC+00:00')
  })

  it('keeps the old free-text offset as the operator wrote it', () => {
    expect(timezoneOffsetLabel('UTC+4', WINTER)).toBe('UTC+4')
    expect(timezoneOffsetLabel('not a zone', WINTER)).toBe('not a zone')
  })
})
