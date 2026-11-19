/**
 * Unit tests for UiStatusIcon: one icon per state, named for assistive technology and on hover, with no visible text.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiStatusIcon from '@/components/ui/UiStatusIcon.vue'
import { Icon } from '@/lib/icons'
import { UiSize } from '@/utils/ui/size'

/** Props shared by every case: a two-factor flag. */
const base = {
  onIcon: Icon.ShieldCheck,
  offIcon: Icon.ShieldOff,
  onLabel: 'Enabled',
  offLabel: 'Disabled',
}

describe('UiStatusIcon', () => {
  it('is an image named by the on label, in the success colour, when the state is on', () => {
    const wrapper = mount(UiStatusIcon, { props: { ...base, state: true } })

    expect(wrapper.attributes('role')).toBe('img')
    expect(wrapper.attributes('aria-label')).toBe('Enabled')
    expect(wrapper.attributes('title')).toBe('Enabled')
    expect(wrapper.classes()).toContain('text-status-green')
    expect(wrapper.findAll('polyline')).toHaveLength(1)
  })

  it('is named by the off label, muted and struck through, when the state is off', () => {
    const wrapper = mount(UiStatusIcon, { props: { ...base, state: false } })

    expect(wrapper.attributes('aria-label')).toBe('Disabled')
    expect(wrapper.attributes('title')).toBe('Disabled')
    expect(wrapper.classes()).toContain('text-text-muted')
    expect(wrapper.classes()).not.toContain('text-status-green')
    expect(wrapper.find('line').exists()).toBe(true)
  })

  it('draws no visible text', () => {
    expect(mount(UiStatusIcon, { props: { ...base, state: true } }).text()).toBe('')
  })

  it('sizes the icon', () => {
    const wrapper = mount(UiStatusIcon, { props: { ...base, state: true, size: UiSize.Lg } })

    expect(wrapper.get('svg').classes()).toContain('w-5')
  })
})
