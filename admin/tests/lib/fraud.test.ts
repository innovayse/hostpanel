/**
 * Tests for the fraud helpers: the table of numeric fields covers every settings field, and the
 * settings check mirrors the API's validator (whole numbers inside each range, review below reject).
 */
import { describe, expect, it } from 'vitest'
import { FRAUD_NUMBER_FIELDS, FRAUD_NOTE_MAX_LENGTH, FraudBand, changedFraudSettingsCount, fraudRiskBands, fraudSettingsErrors } from '@/lib/fraud'
import { FraudSettingsField } from '@/types/fraud'
import type { FraudSettings } from '@/types/fraud'

/** Settings that pass every rule. */
const valid: FraudSettings = {
  enabled: true,
  manualReviewRiskScore: 50,
  autoRejectRiskScore: 80,
  velocityWindowMinutes: 60,
  velocityMaxOrders: 5,
  maxAccountsPerIp: 3,
}

describe('FRAUD_NUMBER_FIELDS', () => {
  it('lists every numeric settings field once, in screen order', () => {
    expect(Object.keys(FRAUD_NUMBER_FIELDS)).toEqual(Object.values(FraudSettingsField))
    expect(Object.entries(FRAUD_NUMBER_FIELDS).every(([name, spec]) => spec.field === name)).toBe(true)
  })

  it('keeps each range ordered and inside the API limits', () => {
    for (const spec of Object.values(FRAUD_NUMBER_FIELDS)) {
      expect(spec.min).toBeLessThan(spec.max)
    }
    const score = FRAUD_NUMBER_FIELDS[FraudSettingsField.ManualReviewRiskScore]
    expect([score.min, score.max]).toEqual([0, 100])
    const window = FRAUD_NUMBER_FIELDS[FraudSettingsField.VelocityWindowMinutes]
    expect([window.min, window.max]).toEqual([1, 1440])
  })

  it('matches the note length the API accepts', () => {
    expect(FRAUD_NOTE_MAX_LENGTH).toBe(500)
  })
})

describe('fraudSettingsErrors', () => {
  it('finds nothing wrong with valid settings', () => {
    expect(fraudSettingsErrors(valid)).toEqual({})
  })

  it('accepts both ends of every range', () => {
    expect(fraudSettingsErrors({ ...valid, manualReviewRiskScore: 0, autoRejectRiskScore: 100 })).toEqual({})
    expect(fraudSettingsErrors({ ...valid, velocityWindowMinutes: 1, velocityMaxOrders: 1, maxAccountsPerIp: 1 })).toEqual({})
    expect(fraudSettingsErrors({ ...valid, velocityWindowMinutes: 1440, velocityMaxOrders: 100, maxAccountsPerIp: 100 })).toEqual({})
  })

  it('flags a score outside 0 to 100', () => {
    const errors = fraudSettingsErrors({ ...valid, manualReviewRiskScore: -1, autoRejectRiskScore: 101 })

    expect(errors.manualReviewRiskScore).toBe('Enter a whole number from 0 to 100.')
    expect(errors.autoRejectRiskScore).toBe('Enter a whole number from 0 to 100.')
  })

  it('flags a number that is not whole', () => {
    expect(fraudSettingsErrors({ ...valid, velocityMaxOrders: 2.5 }).velocityMaxOrders).toBe('Enter a whole number from 1 to 100.')
  })

  it('flags a window and counts outside their ranges', () => {
    const errors = fraudSettingsErrors({ ...valid, velocityWindowMinutes: 1441, velocityMaxOrders: 0, maxAccountsPerIp: 101 })

    expect(errors.velocityWindowMinutes).toBe('Enter a whole number of minutes from 1 to 1440.')
    expect(errors.velocityMaxOrders).toBe('Enter a whole number from 1 to 100.')
    expect(errors.maxAccountsPerIp).toBe('Enter a whole number from 1 to 100.')
  })

  it('requires the review threshold to be below the auto reject threshold', () => {
    expect(fraudSettingsErrors({ ...valid, manualReviewRiskScore: 80, autoRejectRiskScore: 80 }).manualReviewRiskScore).toBe(
      'Must be lower than the auto reject threshold.',
    )
    expect(fraudSettingsErrors({ ...valid, manualReviewRiskScore: 90, autoRejectRiskScore: 40 }).manualReviewRiskScore).toBe(
      'Must be lower than the auto reject threshold.',
    )
  })

  it('judges the order of the thresholds only once both are valid scores', () => {
    const errors = fraudSettingsErrors({ ...valid, manualReviewRiskScore: 150, autoRejectRiskScore: 40 })

    expect(errors.manualReviewRiskScore).toBe('Enter a whole number from 0 to 100.')
  })
})

describe('fraudRiskBands', () => {
  it('cuts the scale at the two thresholds: allow below review, review below reject, reject from there', () => {
    expect(fraudRiskBands(50, 80)).toEqual([
      { band: FraudBand.Allow, from: 0, to: 49, share: 50 },
      { band: FraudBand.Review, from: 50, to: 79, share: 30 },
      { band: FraudBand.Reject, from: 80, to: 100, share: 20 },
    ])
  })

  it('leaves a band empty when a threshold sits on the edge of the scale', () => {
    const [allow, review] = fraudRiskBands(0, 100)

    expect(allow).toMatchObject({ from: 0, to: -1, share: 0 })
    expect(review).toMatchObject({ from: 0, to: 99, share: 100 })
  })

  it('pulls a threshold that is off the scale or not a number onto it, and lifts reject to review when inverted', () => {
    expect(fraudRiskBands(-5, 150).map((segment) => segment.share)).toEqual([0, 100, 0])
    expect(fraudRiskBands(Number.NaN, 40).map((segment) => segment.share)).toEqual([0, 40, 60])
    expect(fraudRiskBands(90, 60).map((segment) => segment.share)).toEqual([90, 0, 10])
  })

  it('always covers the whole scale', () => {
    for (const [review, reject] of [[10, 20], [0, 0], [100, 100], [33.4, 66.6]] as const) {
      expect(fraudRiskBands(review, reject).reduce((total, segment) => total + segment.share, 0)).toBe(100)
    }
  })
})

describe('changedFraudSettingsCount', () => {
  it('is zero for equal settings and counts each setting that differs, the switch included', () => {
    expect(changedFraudSettingsCount(valid, { ...valid })).toBe(0)
    expect(changedFraudSettingsCount({ ...valid, enabled: false }, valid)).toBe(1)
    expect(changedFraudSettingsCount({ ...valid, enabled: false, velocityMaxOrders: 9, manualReviewRiskScore: 10 }, valid)).toBe(3)
  })
})

describe('FRAUD_NUMBER_FIELDS units', () => {
  it('names a unit for every numeric field', () => {
    for (const spec of Object.values(FRAUD_NUMBER_FIELDS)) {
      expect(spec.unitKey).toMatch(/^fraud\.settings\.units\./)
    }
  })
})
