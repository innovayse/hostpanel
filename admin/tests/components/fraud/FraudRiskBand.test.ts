/**
 * Tests for FraudRiskBand: the 0 to 100 scale drawn as the Allow, Review and Reject bands the two thresholds cut, with
 * each band named by the scores it covers.
 */
import { describe, expect, it } from 'vitest'
import FraudRiskBand from '@/components/fraud/FraudRiskBand.vue'
import { mountWithApp } from '../../helpers/mount'

/**
 * The widths of the stretches the bar draws.
 *
 * @param wrapper - The mounted preview.
 * @returns The width of each drawn band, in scale order, as a percentage of the bar.
 */
const widthsOf = (wrapper: ReturnType<typeof mountWithApp>): string[] =>
  wrapper.findAll('[data-testid="ui-chart-distribution-part"]').map(part => (part.element.parentElement as HTMLElement).style.width)

/**
 * The captions under the bar.
 *
 * @param wrapper - The mounted preview.
 * @returns The caption of each band, in scale order.
 */
const captionsOf = (wrapper: ReturnType<typeof mountWithApp>): string[] =>
  wrapper.findAll('[data-testid="ui-chart-legend"] li').map(item => item.text())

describe('FraudRiskBand', () => {
  it('draws the three bands side by side over the whole scale', () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 50, reject: 80 } })

    expect(widthsOf(wrapper)).toEqual(['50%', '30%', '20%'])
  })

  it('names each band with the scores it covers', () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 50, reject: 80 } })

    expect(captionsOf(wrapper)).toEqual(['Allow, 0 to 49', 'Review, 50 to 79', 'Reject, 80 to 100'])
  })

  it('says none for a band the thresholds leave empty and draws no stretch for it', () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 0, reject: 60 } })

    expect(captionsOf(wrapper)[0]).toBe('Allow, none')
    expect(widthsOf(wrapper)).toEqual(['60%', '40%'])
  })

  it('follows new thresholds', async () => {
    const wrapper = mountWithApp(FraudRiskBand, { props: { review: 50, reject: 80 } })

    await wrapper.setProps({ review: 20, reject: 90 })

    expect(widthsOf(wrapper)).toEqual(['20%', '70%', '10%'])
  })
})
