/**
 * Helpers of the fraud-detection area, one file for the area: the limits the fraud screens share
 * with the API's validators, the table of the numeric settings fields, and the settings check that
 * spares the operator a round trip. The sentences of the area's closed value sets are option lists
 * in `constants.ts`.
 */
import { translate } from '@/lib/translate'
import { FraudSettingsField } from '@/types/fraud'
import type { FraudSettings } from '@/types/fraud'

/** Lowest risk score a threshold may take. Mirrors the API's validator. */
const FRAUD_SCORE_MIN = 0

/** Highest risk score a threshold may take. Mirrors the API's validator. */
const FRAUD_SCORE_MAX = 100

/** Shortest order-velocity window, in minutes. Mirrors the API's validator. */
const FRAUD_WINDOW_MIN_MINUTES = 1

/** Longest order-velocity window, in minutes (one day). Mirrors the API's validator. */
const FRAUD_WINDOW_MAX_MINUTES = 1440

/** Fewest orders or accounts a limit may allow. Mirrors the API's validator. */
const FRAUD_COUNT_MIN = 1

/** Most orders or accounts a limit may allow. Mirrors the API's validator. */
const FRAUD_COUNT_MAX = 100

/** Longest note a block list entry may carry. Mirrors the API's validator. */
export const FRAUD_NOTE_MAX_LENGTH = 500

/** One numeric field of the fraud settings: its allowed range and the sentence shown outside it. */
export interface FraudNumberField {
  /** The field, named as the API names it. */
  field: FraudSettingsField
  /** Lowest allowed value. */
  min: number
  /** Highest allowed value. */
  max: number
  /** Message key of the sentence shown when the value is not a whole number inside the range. */
  rangeMessageKey: string
}

/** The numeric fields of the fraud settings, in the order the screen draws them. */
export const FRAUD_NUMBER_FIELDS: readonly FraudNumberField[] = [
  {
    field: FraudSettingsField.ManualReviewRiskScore,
    min: FRAUD_SCORE_MIN,
    max: FRAUD_SCORE_MAX,
    rangeMessageKey: 'fraud.settings.errors.scoreRange',
  },
  {
    field: FraudSettingsField.AutoRejectRiskScore,
    min: FRAUD_SCORE_MIN,
    max: FRAUD_SCORE_MAX,
    rangeMessageKey: 'fraud.settings.errors.scoreRange',
  },
  {
    field: FraudSettingsField.VelocityWindowMinutes,
    min: FRAUD_WINDOW_MIN_MINUTES,
    max: FRAUD_WINDOW_MAX_MINUTES,
    rangeMessageKey: 'fraud.settings.errors.windowRange',
  },
  {
    field: FraudSettingsField.VelocityMaxOrders,
    min: FRAUD_COUNT_MIN,
    max: FRAUD_COUNT_MAX,
    rangeMessageKey: 'fraud.settings.errors.countRange',
  },
  {
    field: FraudSettingsField.MaxAccountsPerIp,
    min: FRAUD_COUNT_MIN,
    max: FRAUD_COUNT_MAX,
    rangeMessageKey: 'fraud.settings.errors.countRange',
  },
]

/**
 * Checks the fraud settings the way the API's validator does, so a refused value is shown before
 * the request is sent.
 *
 * @param settings - The settings as edited.
 * @returns The sentence of each invalid field; empty when every field is valid.
 */
export const fraudSettingsErrors = (settings: FraudSettings): Partial<Record<FraudSettingsField, string>> => {
  const errors: Partial<Record<FraudSettingsField, string>> = {}
  for (const { field, min, max, rangeMessageKey } of FRAUD_NUMBER_FIELDS) {
    const value = settings[field]
    if (!Number.isInteger(value) || value < min || value > max) {
      errors[field] = translate(rangeMessageKey)
    }
  }
  // The order of the two thresholds is only judged once each is a valid score, as the API does.
  if (
    errors[FraudSettingsField.ManualReviewRiskScore] === undefined &&
    errors[FraudSettingsField.AutoRejectRiskScore] === undefined &&
    settings.manualReviewRiskScore >= settings.autoRejectRiskScore
  ) {
    errors[FraudSettingsField.ManualReviewRiskScore] = translate('fraud.settings.errors.reviewBelowReject')
  }
  return errors
}
