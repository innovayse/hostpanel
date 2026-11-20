/**
 * Unit tests for PluginOverviewFact: a caption over a value, over the slot when one is given, and over the empty text
 * when the value is missing.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginOverviewFact from '@/components/plugins/details/PluginOverviewFact.vue'

describe('PluginOverviewFact', () => {
  it('shows the caption over the value', () => {
    const wrapper = mount(PluginOverviewFact, { props: { label: 'Author', value: 'Hostpanel' } })

    expect(wrapper.text()).toContain('Author')
    expect(wrapper.text()).toContain('Hostpanel')
  })

  it('shows the empty text in place of a missing value', () => {
    const wrapper = mount(PluginOverviewFact, { props: { label: 'Installed', value: null, empty: 'Not recorded' } })

    expect(wrapper.text()).toContain('Not recorded')
  })

  it('draws a monospace value that breaks anywhere when asked to', () => {
    const plain = mount(PluginOverviewFact, { props: { label: 'Digest', value: 'abc' } })
    const mono = mount(PluginOverviewFact, { props: { label: 'Digest', value: 'abc', mono: true } })

    expect(plain.html()).not.toContain('font-mono')
    expect(mono.html()).toContain('font-mono')
    expect(mono.html()).toContain('break-all')
  })

  it('draws the slot in place of the value', () => {
    const wrapper = mount(PluginOverviewFact, { props: { label: 'State', value: 'ignored' }, slots: { default: '<strong>Custom</strong>' } })

    expect(wrapper.find('strong').text()).toBe('Custom')
    expect(wrapper.text()).not.toContain('ignored')
  })
})
