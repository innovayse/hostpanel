/**
 * Tests for DashboardChartCard: the title, the optional note, the aside slot and the body.
 */
import { describe, expect, it } from 'vitest'
import DashboardChartCard from '@/components/dashboard/DashboardChartCard.vue'
import { mountWithApp } from '../../helpers/mount'

describe('DashboardChartCard', () => {
  it('shows the title, the note, the aside figure and the body', () => {
    const wrapper = mountWithApp(DashboardChartCard, {
      props: { title: 'Card title', note: 'Card note' },
      slots: { aside: '<span>aside figure</span>', default: '<p>chart body</p>' },
    })

    expect(wrapper.text()).toContain('Card title')
    expect(wrapper.text()).toContain('Card note')
    expect(wrapper.text()).toContain('aside figure')
    expect(wrapper.text()).toContain('chart body')
  })

  it('leaves the note out when there is none', () => {
    const wrapper = mountWithApp(DashboardChartCard, { props: { title: 'Card title' } })

    expect(wrapper.text()).toBe('Card title')
  })
})
