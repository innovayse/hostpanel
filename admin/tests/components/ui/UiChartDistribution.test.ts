/**
 * Tests for UiChartDistribution: parts as wide as their share, empty parts left out, a dash for an empty bar.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChartDistribution from '@/components/ui/UiChartDistribution.vue'
import { UiTone } from '@/utils/ui/tone'

describe('UiChartDistribution', () => {
  it('draws a part per non-empty count with its tooltip and width', () => {
    const wrapper = mount(UiChartDistribution, {
      props: {
        parts: [
          { count: 1, tone: UiTone.Danger, tooltip: 'Poor: 1' },
          { count: 0, tone: UiTone.Warning, tooltip: 'Fair: 0' },
          { count: 3, tone: UiTone.Success, tooltip: 'Good: 3' },
        ],
      },
    })

    expect(wrapper.findAll('[data-testid="ui-chart-distribution-part"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Poor: 1')
    expect(wrapper.text()).not.toContain('Fair: 0')
    expect(wrapper.html()).toContain('width: 75%')
  })

  it('fills the width of its parent when asked to, a table cell width otherwise', () => {
    const parts = [{ count: 1, tone: UiTone.Success, tooltip: 'Good: 1' }]

    expect(mount(UiChartDistribution, { props: { parts, wide: true } }).get('[data-testid="ui-chart-distribution"]').classes()).toContain('w-full')
    expect(mount(UiChartDistribution, { props: { parts } }).get('[data-testid="ui-chart-distribution"]').classes()).toContain('w-40')
  })

  it('keeps a readable width and scrolls sideways inside its box on a phone when wide', () => {
    const parts = [{ count: 1, tone: UiTone.Success, tooltip: 'Good: 1' }]
    const wide = mount(UiChartDistribution, { props: { parts, wide: true } })
    const fixed = mount(UiChartDistribution, { props: { parts } })

    expect(wide.get('[data-testid="ui-chart-distribution"]').classes()).toContain('max-sm:overflow-x-auto')
    expect(wide.get('[data-testid="ui-chart-distribution"] > div').classes()).toContain('max-sm:min-w-72')
    expect(fixed.get('[data-testid="ui-chart-distribution"]').classes()).not.toContain('max-sm:overflow-x-auto')
    expect(fixed.get('[data-testid="ui-chart-distribution"] > div').classes()).not.toContain('max-sm:min-w-72')
  })

  it('shows a dash when nothing is counted', () => {
    const wrapper = mount(UiChartDistribution, { props: { parts: [{ count: 0, tone: UiTone.Danger, tooltip: 'Poor: 0' }] } })

    expect(wrapper.find('[data-testid="ui-chart-distribution"]').exists()).toBe(false)
    expect(wrapper.text()).toBe('—')
  })
})
