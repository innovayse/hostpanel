/**
 * Unit tests for UiSearchTrigger: a labelled button that is an icon when narrow and a bar with the text and the
 * shortcut when its container is wide.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSearchTrigger from '@/components/ui/UiSearchTrigger.vue'

describe('UiSearchTrigger', () => {
  it('is a button named by its label, with an icon', () => {
    const wrapper = mount(UiSearchTrigger, { props: { label: 'Search the panel', text: 'Search pages' } })

    expect(wrapper.element.tagName).toBe('BUTTON')
    expect(wrapper.attributes('type')).toBe('button')
    expect(wrapper.attributes('aria-label')).toBe('Search the panel')
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('is the bare icon of the other triggers when narrow and the field-like bar from 4xl', () => {
    const wrapper = mount(UiSearchTrigger, { props: { label: 'Search', text: 'Search pages' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-control', 'bg-transparent', 'border-transparent', 'w-10.5']))
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['@4xl:bg-surface-field', '@4xl:max-w-[460px]', '@4xl:flex-1']))
    expect(wrapper.get('p').text()).toBe('Search pages')
    expect(wrapper.get('p').classes()).toEqual(expect.arrayContaining(['hidden', '@4xl:block']))
  })

  it('announces the shortcut only from 5xl, and draws nothing without one', () => {
    const withShortcut = mount(UiSearchTrigger, { props: { label: 'Search', text: 'Search pages', shortcut: 'Ctrl K', shortcutTestid: 'chip' } })
    const without = mount(UiSearchTrigger, { props: { label: 'Search', text: 'Search pages' } })

    expect(withShortcut.get('[data-testid="chip"]').text()).toBe('Ctrl K')
    expect(withShortcut.get('[data-testid="chip"]').classes()).toEqual(expect.arrayContaining(['hidden', '@5xl:block']))
    expect(without.findAll('p')).toHaveLength(1)
  })

  it('passes data attributes and listeners to the button', async () => {
    const wrapper = mount(UiSearchTrigger, { props: { label: 'Search', text: 'Search pages' }, attrs: { 'data-testid': 'probe' } })

    await wrapper.trigger('click')

    expect(wrapper.attributes('data-testid')).toBe('probe')
    expect(wrapper.emitted('click')).toHaveLength(1)
  })
})
