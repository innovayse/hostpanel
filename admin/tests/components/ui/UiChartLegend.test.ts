/**
 * Tests for UiChartLegend: one captioned dot per entry, in order.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChartLegend from '@/components/ui/UiChartLegend.vue'
import { UiTone } from '@/utils/ui/tone'

describe('UiChartLegend', () => {
  it('lists the captions in order with the dot of each tone', () => {
    const wrapper = mount(UiChartLegend, {
      props: { items: [{ tone: UiTone.Danger, label: 'Poor' }, { tone: UiTone.Success, label: 'Good' }] },
    })

    expect(wrapper.findAll('li').map(item => item.text())).toEqual(['Poor', 'Good'])
    expect(wrapper.find('span').classes()).toContain('bg-status-red')
  })
})
