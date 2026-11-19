/**
 * Tests for the time zone helpers: the offset and city labels, the legacy `UTC+4` reading, and the one list of IANA
 * zones the picker offers, grouped by region.
 */
import { describe, expect, it } from 'vitest'
import { isKnownTimezone, legacyOffsetZone, timezoneIdOf, timezoneLabel, timezoneOptions, utcOffsetLabel } from '@/lib/timezones'

/** A winter day, so a zone with daylight saving time is on its standard offset. */
const WINTER = new Date('2026-01-15T12:00:00Z')

/** A summer day, so a zone with daylight saving time is on its summer offset. */
const SUMMER = new Date('2026-07-15T12:00:00Z')

describe('utcOffsetLabel', () => {
  it('writes the offset as UTC plus hours and minutes', () => {
    expect(utcOffsetLabel('Asia/Yerevan', WINTER)).toBe('UTC+04:00')
    expect(utcOffsetLabel('America/New_York', WINTER)).toBe('UTC-05:00')
    expect(utcOffsetLabel('Asia/Kolkata', WINTER)).toBe('UTC+05:30')
  })

  it('follows daylight saving time', () => {
    expect(utcOffsetLabel('Europe/Berlin', WINTER)).toBe('UTC+01:00')
    expect(utcOffsetLabel('Europe/Berlin', SUMMER)).toBe('UTC+02:00')
  })

  it('writes a zero offset as UTC+00:00', () => {
    expect(utcOffsetLabel('UTC', WINTER)).toBe('UTC+00:00')
  })
})

describe('timezoneLabel', () => {
  it('is the city and the current offset', () => {
    expect(timezoneLabel('Asia/Yerevan', WINTER)).toBe('Yerevan (UTC+04:00)')
    expect(timezoneLabel('America/Argentina/Buenos_Aires', WINTER)).toBe('Buenos Aires (UTC-03:00)')
  })

  it('is the offset alone for a zone that has no city', () => {
    expect(timezoneLabel('UTC', WINTER)).toBe('UTC+00:00')
    expect(timezoneLabel('Etc/GMT-4', WINTER)).toBe('UTC+04:00')
  })
})

describe('legacyOffsetZone', () => {
  it('reads the old free-text offset as the fixed-offset zone that means the same, the Etc zones counting the other way round', () => {
    expect(legacyOffsetZone('UTC+4')).toBe('Etc/GMT-4')
    expect(legacyOffsetZone('GMT-3')).toBe('Etc/GMT+3')
    expect(legacyOffsetZone(' utc + 05 ')).toBe('Etc/GMT-5')
  })

  it('reads a zero offset as UTC', () => {
    expect(legacyOffsetZone('UTC+0')).toBe('UTC')
  })

  it('refuses what the Etc zones do not cover', () => {
    expect(legacyOffsetZone('UTC+15')).toBeNull()
    expect(legacyOffsetZone('UTC-13')).toBeNull()
    expect(legacyOffsetZone('UTC+5:30')).toBeNull()
    expect(legacyOffsetZone('Asia/Yerevan')).toBeNull()
  })
})

describe('timezoneIdOf', () => {
  it('keeps an IANA id, maps a legacy offset and leaves other text as it is', () => {
    expect(timezoneIdOf('Asia/Yerevan')).toBe('Asia/Yerevan')
    expect(timezoneIdOf('UTC+4')).toBe('Etc/GMT-4')
    expect(timezoneIdOf('somewhere')).toBe('somewhere')
    expect(timezoneIdOf('')).toBe('')
  })
})

describe('isKnownTimezone', () => {
  it('accepts an IANA id and refuses anything else', () => {
    expect(isKnownTimezone('Asia/Yerevan')).toBe(true)
    expect(isKnownTimezone('Etc/GMT-4')).toBe(true)
    expect(isKnownTimezone('UTC+4')).toBe(false)
    expect(isKnownTimezone('')).toBe(false)
  })
})

describe('timezoneOptions', () => {
  const options = timezoneOptions('en', WINTER)

  it('lists UTC and every zone the engine knows, each once', () => {
    const values = options.map((option) => option.value)

    expect(values).toContain('UTC')
    expect(values).toContain('Asia/Yerevan')
    expect(new Set(values).size).toBe(values.length)
  })

  it('labels a row with its city and offset, and lets the id be searched', () => {
    const yerevan = options.find((option) => option.value === 'Asia/Yerevan')

    expect(yerevan?.label).toMatch(/^Yerevan \(UTC\+04:00\)$/)
    expect(yerevan?.keywords).toBe('Asia/Yerevan')
    expect(yerevan?.description).toBeTruthy()
  })

  it('groups the rows by region, in one run per region', () => {
    const groups = options.map((option) => option.group)
    const runs = groups.filter((group, index) => group !== groups[index - 1])

    expect(new Set(runs).size).toBe(runs.length)
    expect(runs.length).toBeGreaterThan(5)
  })

  it('orders the rows of a region by offset', () => {
    const asia = options.filter((option) => option.value.startsWith('Asia/'))
    const offsets = asia.map((option) => Number(/UTC([+-]\d{2}):(\d{2})/.exec(option.label)?.[1]))

    expect(offsets).toEqual([...offsets].sort((a, b) => a - b))
  })

  it('is built once per language', () => {
    expect(timezoneOptions('en', WINTER)).toBe(options)
  })

  it('reads the offsets again when the hour changes, so daylight saving time shows', () => {
    const summer = timezoneOptions('en', new Date('2026-07-15T12:00:00Z'))
    /**
     * The label of Berlin in a list of options.
     *
     * @param list - The options to search.
     * @returns Berlin's label, or undefined when the list has none.
     */
    const berlin = (list: typeof options): string | undefined => list.find((option) => option.value === 'Europe/Berlin')?.label

    expect(berlin(options)).toBe('Berlin (UTC+01:00)')
    expect(berlin(summer)).toBe('Berlin (UTC+02:00)')
    expect(timezoneOptions('en', WINTER)).not.toBe(summer)
  })
})
