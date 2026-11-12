/**
 * Tests for the fraud helpers: the table of numeric fields covers every settings field, and the
 * settings check mirrors the API's validator (whole numbers inside each range, review below reject).
 */
import { describe, expect, it } from 'vitest'
import { FRAUD_NUMBER_FIELDS, FRAUD_NOTE_MAX_LENGTH, fraudSettingsErrors } from '@/lib/fraud'
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
    expect(FRAUD_NUMBER_FIELDS.map((spec) => spec.field)).toEqual(Object.values(FraudSettingsField))
  })

  it('keeps each range ordered and inside the API limits', () => {
    for (const spec of FRAUD_NUMBER_FIELDS) {
      expect(spec.min).toBeLessThan(spec.max)
    }
    const score = FRAUD_NUMBER_FIELDS.find((spec) => spec.field === FraudSettingsField.ManualReviewRiskScore)
    expect([score?.min, score?.max]).toEqual([0, 100])
    const window = FRAUD_NUMBER_FIELDS.find((spec) => spec.field === FraudSettingsField.VelocityWindowMinutes)
    expect([window?.min, window?.max]).toEqual([1, 1440])
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
