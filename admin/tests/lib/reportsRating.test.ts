/**
 * Tests for the tone of a support rating.
 */
import { describe, expect, it } from 'vitest'
import { bandCounts, RATING_BANDS, RatingBand, ratingTone } from '@/lib/reportsRating'
import { UiTone } from '@/utils/ui/tone'

describe('ratingTone', () => {
  it('is success from 8, warning from 5 and danger below', () => {
    expect(ratingTone(10)).toBe(UiTone.Success)
    expect(ratingTone(8)).toBe(UiTone.Success)
    expect(ratingTone(7)).toBe(UiTone.Warning)
    expect(ratingTone(5)).toBe(UiTone.Warning)
    expect(ratingTone(4)).toBe(UiTone.Danger)
  })
})

describe('bandCounts', () => {
  it('adds the counters of ratings 1 to 10 into the poor, fair and good bands', () => {
    expect(RATING_BANDS.map(({ band }) => band)).toEqual([RatingBand.Poor, RatingBand.Fair, RatingBand.Good])
    expect(bandCounts([1, 1, 1, 1, 2, 2, 2, 3, 3, 3])).toEqual([4, 6, 9])
  })
})
