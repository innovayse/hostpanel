/**
 * Tests for FraudRiskBand: the 0 to 100 scale drawn as the Allow, Review and Reject bands the two thresholds cut, with
 * each band named by the scores it covers.
 */
import { describe, expect, it } from 'vitest'
import FraudRiskBand from '@/components/fraud/FraudRiskBand.vue'
import { mountWithApp } from '../../helpers/mount'

/**
 * The stretch of the bar one band takes.
 *
 * @param wrapper - The mounted preview.
 * @param band - The band's wire name.
 * @returns The left edge and the width of the band's rectangle.
 */
const stretchOf = (wrapper: ReturnType<typeof mountWithApp>, band: string): { x: string | undefined; width: string | undefined } => {
  const rect = wrapper.get(`[data-testid="fraud-risk-band-${band}"]`)
  return { x: rect.attributes('x'), width: rect.attributes('width') }
}

describe('FraudRiskBand', () => {
  it('draws the three bands side by side over the whole scale', () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 50, reject: 80 } })

    expect(stretchOf(wrapper, 'allow')).toEqual({ x: '0', width: '50' })
    expect(stretchOf(wrapper, 'review')).toEqual({ x: '50', width: '30' })
    expect(stretchOf(wrapper, 'reject')).toEqual({ x: '80', width: '20' })
    expect(wrapper.get('svg').attributes('viewBox')).toBe('0 0 100 1')
  })

  it('names each band with the scores it covers', () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 50, reject: 80 } })

    expect(wrapper.get('[data-testid="fraud-risk-band-caption-allow"]').text()).toBe('Allow, 0 to 49')
    expect(wrapper.get('[data-testid="fraud-risk-band-caption-review"]').text()).toBe('Review, 50 to 79')
    expect(wrapper.get('[data-testid="fraud-risk-band-caption-reject"]').text()).toBe('Reject, 80 to 100')
  })

  it('says none for a band the thresholds leave empty', () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 0, reject: 60 } })

    expect(wrapper.get('[data-testid="fraud-risk-band-caption-allow"]').text()).toBe('Allow, none')
  })

  it('follows new thresholds', async () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 50, reject: 80 } })

    await wrapper.setProps({ review: 20, reject: 90 })

    expect(stretchOf(wrapper, 'review')).toEqual({ x: '20', width: '70' })
  })

  it('is one image for assistive technology, described by the three sentences', () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 50, reject: 80 } })

    expect(wrapper.get('svg').attributes('role')).toBe('img')
    expect(wrapper.get('svg').attributes('aria-label')).toBe('Allow, 0 to 49. Review, 50 to 79. Reject, 80 to 100')
  })
})
