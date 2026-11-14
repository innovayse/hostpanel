/**
 * Unit tests for UiTooltip: the hint is linked to the trigger and revealed on hover or focus.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { FLOATING_PANEL_CLASSES } from '@/utils/ui/floating'

describe('UiTooltip', () => {
  it('renders the trigger and the hint', () => {
    const wrapper = mount(UiTooltip, { props: { text: 'Delete' }, slots: { default: '<button>x</button>' } })

    expect(wrapper.find('button').exists()).toBe(true)
    expect(wrapper.get('[role="tooltip"]').text()).toBe('Delete')
  })

  it('draws the hint on the opaque floating surface, never glass', () => {
    const classes = mount(UiTooltip, { props: { text: 'Delete' } }).get('[role="tooltip"]').classes()

    expect(classes).toEqual(expect.arrayContaining(FLOATING_PANEL_CLASSES.split(' ')))
    expect(classes.some((name) => name.includes('glass'))).toBe(false)
  })

  it('links the hint to the trigger wrapper', () => {
    const wrapper = mount(UiTooltip, { props: { text: 'Delete' } })

    expect(wrapper.attributes('aria-describedby')).toBe(wrapper.get('[role="tooltip"]').attributes('id'))
  })

  it('is revealed by hover and focus classes and placed by the placement', () => {
    const top = mount(UiTooltip, { props: { text: 'a' } }).get('[role="tooltip"]')
    const bottom = mount(UiTooltip, { props: { text: 'a', placement: 'bottom' } }).get('[role="tooltip"]')

    expect(top.classes()).toEqual(expect.arrayContaining(['hidden', 'group-hover:block', 'group-focus-within:block', 'bottom-full']))
    expect(bottom.classes()).toContain('top-full')
  })
})
