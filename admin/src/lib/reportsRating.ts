/**
 * Tone of a support rating, shared by the ticket feedback reports.
 */
import { UiTone } from '@/utils/ui/tone'

/** Rating from which feedback counts as good. */
const GOOD_RATING = 8

/** Rating from which feedback counts as acceptable. */
const FAIR_RATING = 5

/**
 * Picks the badge tone of a rating from 1 to 10.
 *
 * @param rating - The rating.
 * @returns Success for a good rating, warning for a fair one, danger below that.
 */
export const ratingTone = (rating: number): UiTone => {
  if (rating >= GOOD_RATING) {
    return UiTone.Success
  }
  return rating >= FAIR_RATING ? UiTone.Warning : UiTone.Danger
}

/** Closed set of the bands the ratings from 1 to 10 are grouped in for a distribution. */
export const RatingBand = {
  Poor: 'poor',
  Fair: 'fair',
  Good: 'good',
} as const

/** One of the rating bands. */
export type RatingBand = (typeof RatingBand)[keyof typeof RatingBand]

/** The bands from the lowest ratings up, each with its tone and the first and last rating it holds. */
export const RATING_BANDS: readonly { band: RatingBand; tone: UiTone; from: number; to: number }[] = [
  { band: RatingBand.Poor, tone: UiTone.Danger, from: 1, to: FAIR_RATING - 1 },
  { band: RatingBand.Fair, tone: UiTone.Warning, from: FAIR_RATING, to: GOOD_RATING - 1 },
  { band: RatingBand.Good, tone: UiTone.Success, from: GOOD_RATING, to: 10 },
]

/**
 * Adds up the counters of ratings 1 to 10 into one count per band.
 *
 * @param counts - How many ratings each score received, index 0 holding rating 1.
 * @returns The count of each band, in the order of {@link RATING_BANDS}.
 */
export const bandCounts = (counts: readonly number[]): number[] =>
  RATING_BANDS.map(({ from, to }) => counts.slice(from - 1, to).reduce((sum, count) => sum + count, 0))
