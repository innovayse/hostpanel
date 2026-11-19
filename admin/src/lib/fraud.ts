/**
 * Helpers of the fraud-detection area, one file for the area: the limits the fraud screens share
 * with the API's validators, the table of the numeric settings fields, the settings check that
 * spares the operator a round trip, and the bands of the risk scale the thresholds cut. The sentences of the area's closed value sets are option lists
 * in `constants.ts`.
 */
import { translate } from '@/lib/translate'
import { FraudSettingsField } from '@/types/fraud'
import type { FraudSettings } from '@/types/fraud'

/** Lowest risk score a threshold may take. Mirrors the API's validator. */
const FRAUD_SCORE_MIN = 0

/** Highest risk score a threshold may take. Mirrors the API's validator. */
export const FRAUD_SCORE_MAX = 100

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
  /** Message key of the unit written inside the input after the number. */
  unitKey: string
}

/** The numeric fields of the fraud settings by name, in the order the screen draws them. */
export const FRAUD_NUMBER_FIELDS: Readonly<Record<FraudSettingsField, FraudNumberField>> = {
  [FraudSettingsField.ManualReviewRiskScore]: {
    field: FraudSettingsField.ManualReviewRiskScore,
    min: FRAUD_SCORE_MIN,
    max: FRAUD_SCORE_MAX,
    rangeMessageKey: 'fraud.settings.errors.scoreRange',
    unitKey: 'fraud.settings.units.points',
  },
  [FraudSettingsField.AutoRejectRiskScore]: {
    field: FraudSettingsField.AutoRejectRiskScore,
    min: FRAUD_SCORE_MIN,
    max: FRAUD_SCORE_MAX,
    rangeMessageKey: 'fraud.settings.errors.scoreRange',
    unitKey: 'fraud.settings.units.points',
  },
  [FraudSettingsField.VelocityWindowMinutes]: {
    field: FraudSettingsField.VelocityWindowMinutes,
    min: FRAUD_WINDOW_MIN_MINUTES,
    max: FRAUD_WINDOW_MAX_MINUTES,
    rangeMessageKey: 'fraud.settings.errors.windowRange',
    unitKey: 'fraud.settings.units.minutes',
  },
  [FraudSettingsField.VelocityMaxOrders]: {
    field: FraudSettingsField.VelocityMaxOrders,
    min: FRAUD_COUNT_MIN,
    max: FRAUD_COUNT_MAX,
    rangeMessageKey: 'fraud.settings.errors.countRange',
    unitKey: 'fraud.settings.units.orders',
  },
  [FraudSettingsField.MaxAccountsPerIp]: {
    field: FraudSettingsField.MaxAccountsPerIp,
    min: FRAUD_COUNT_MIN,
    max: FRAUD_COUNT_MAX,
    rangeMessageKey: 'fraud.settings.errors.countRange',
    unitKey: 'fraud.settings.units.accounts',
  },
}

/**
 * Checks the fraud settings the way the API's validator does, so a refused value is shown before
 * the request is sent.
 *
 * @param settings - The settings as edited.
 * @returns The sentence of each invalid field; empty when every field is valid.
 */
export const fraudSettingsErrors = (settings: FraudSettings): Partial<Record<FraudSettingsField, string>> => {
  const errors: Partial<Record<FraudSettingsField, string>> = {}
  for (const { field, min, max, rangeMessageKey } of Object.values(FRAUD_NUMBER_FIELDS)) {
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

/** The names of every fraud setting the screen edits, the switch included, in the order the form draws them. */
const FRAUD_SETTING_NAMES: readonly (keyof FraudSettings)[] = ['enabled', ...Object.values(FraudSettingsField)]

/**
 * Counts the settings an edited draft holds differently from the stored ones.
 *
 * @param draft - The settings as edited.
 * @param stored - The settings as the API holds them.
 * @returns How many of the settings differ; zero when there is nothing to save.
 */
export const changedFraudSettingsCount = (draft: FraudSettings, stored: FraudSettings): number =>
  FRAUD_SETTING_NAMES.filter((name) => draft[name] !== stored[name]).length

/** What the fraud check does with an action whose risk score falls in a band of the scale. */
export enum FraudBand {
  /** Below the review threshold: the action goes through. */
  Allow = 'allow',
  /** From the review threshold, below the reject threshold: the order waits for staff. */
  Review = 'review',
  /** From the reject threshold: the action is refused at once. */
  Reject = 'reject',
}

/** One band of the 0 to 100 risk scale, cut by the two thresholds. */
export interface FraudBandSegment {
  /** The band. */
  band: FraudBand
  /** Lowest score of the band. */
  from: number
  /** Highest score of the band; below `from` when the band is empty. */
  to: number
  /** Share of the scale the band takes, 0 to 100. */
  share: number
}

/**
 * Pulls a threshold onto the scale.
 *
 * @param value - The threshold as typed.
 * @returns The nearest whole score from 0 to 100; 0 for a value that is not a number.
 */
const onScale = (value: number): number =>
  Math.min(FRAUD_SCORE_MAX, Math.max(FRAUD_SCORE_MIN, Math.round(Number.isFinite(value) ? value : FRAUD_SCORE_MIN)))

/**
 * Cuts the 0 to 100 risk scale into its three bands at the two thresholds, the way the decision policy reads them.
 *
 * A threshold that is not a whole number inside the scale is pulled onto it, and a reject threshold below the review
 * threshold is lifted to it, so the preview always draws a scale; the form flags the bad value separately.
 *
 * @param review - The review threshold: the first score that waits for staff.
 * @param reject - The reject threshold: the first score that is refused at once.
 * @returns The bands in scale order, empty ones included (their `to` is below their `from`).
 */
export const fraudRiskBands = (review: number, reject: number): FraudBandSegment[] => {
  const reviewAt = onScale(review)
  const rejectAt = Math.max(reviewAt, onScale(reject))
  return [
    { band: FraudBand.Allow, from: FRAUD_SCORE_MIN, to: reviewAt - 1, share: reviewAt - FRAUD_SCORE_MIN },
    { band: FraudBand.Review, from: reviewAt, to: rejectAt - 1, share: rejectAt - reviewAt },
    { band: FraudBand.Reject, from: rejectAt, to: FRAUD_SCORE_MAX, share: FRAUD_SCORE_MAX - rejectAt },
  ]
}
