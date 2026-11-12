/**
 * Unit tests for UiRating: five stars with the rounded average filled, the average and the review
 * count beside them, and one spoken sentence for the whole row.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiRating from '@/components/ui/UiRating.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the rating.
 *
 * @param average - Average score.
 * @param count - Review count.
 * @returns The wrapper.
 */
const mountRating = (average: number, count: number) =>
  mount(UiRating, { props: { average, count }, global: { plugins: [createTestI18n()] } })

describe('UiRating', () => {
  it('draws five stars and fills the average rounded to a whole star', () => {
    const stars = mountRating(3.6, 12).findAll('[data-testid="rating-star"]')

    expect(stars).toHaveLength(5)
    expect(stars.map(star => star.attributes('data-on'))).toEqual(['true', 'true', 'true', 'true', 'false'])
  })

  it('shows the average with one decimal and the review count', () => {
    expect(mountRating(4, 7).get('[data-testid="rating-summary"]').text()).toBe('4.0 (7)')
  })

  it('reads one sentence for the whole row', () => {
    const row = mountRating(4.5, 12).get('[data-testid="rating"]')

    expect(row.attributes('role')).toBe('img')
    expect(row.attributes('aria-label')).toBe('Rated 4.5 out of 5 from 12 reviews')
  })

  it('reads the singular for a single review', () => {
    expect(mountRating(5, 1).get('[data-testid="rating"]').attributes('aria-label')).toBe('Rated 5.0 out of 5 from 1 review')
  })

  it('keeps the filled stars inside the row for an out of range average', () => {
    const filledOf = (average: number) =>
      mountRating(average, 1).findAll('[data-testid="rating-star"]').filter(star => star.attributes('data-on') === 'true').length

    expect(filledOf(7)).toBe(5)
    expect(filledOf(-1)).toBe(0)
  })
})
