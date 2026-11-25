/**
 * Unit tests for UiStatusBadge: a boolean state is a labelled badge, never a bare icon.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiStatusBadge from '@/components/ui/UiStatusBadge.vue'
import { UiTone } from '@/utils/ui/tone'

/** Props shared by every case: a publishing flag. */
const base = {
  onLabel: 'Published',
  offLabel: 'Draft',
}

describe('UiStatusBadge', () => {
  it('shows the on label in the success tone when the state is on', () => {
    const wrapper = mount(UiStatusBadge, { props: { ...base, state: true } })

    expect(wrapper.text()).toBe('Published')
    expect(wrapper.getComponent(UiBadge).props('tone')).toBe(UiTone.Success)
  })

  it('shows the off label in the neutral tone when the state is off', () => {
    const wrapper = mount(UiStatusBadge, { props: { ...base, state: false } })

    expect(wrapper.text()).toBe('Draft')
    expect(wrapper.getComponent(UiBadge).props('tone')).toBe(UiTone.Neutral)
  })

  it('draws no icon', () => {
    expect(mount(UiStatusBadge, { props: { ...base, state: true } }).find('svg').exists()).toBe(false)
  })
})
