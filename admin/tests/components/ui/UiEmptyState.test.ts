/**
 * Unit tests for UiEmptyState: title, description, the framed icon and the actions slot, and the
 * layout staying free of empty gaps when a slot is not given.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'

describe('UiEmptyState', () => {
  it('renders the title as a heading and the description', () => {
    const wrapper = mount(UiEmptyState, { props: { title: 'No invoices', description: 'Create the first one' } })

    expect(wrapper.get('h3').text()).toBe('No invoices')
    expect(wrapper.text()).toContain('Create the first one')
  })

  it('omits the description when none is given', () => {
    const wrapper = mount(UiEmptyState, { props: { title: 'Nothing' } })

    expect(wrapper.find('p').exists()).toBe(false)
  })

  it('renders the icon in a frosted round tile and the actions slot', () => {
    const wrapper = mount(UiEmptyState, {
      props: { title: 'Nothing' },
      slots: { icon: '<i data-testid="icon" />', actions: '<b data-testid="action" />' },
    })

    const tile = wrapper.get('[data-testid="ui-empty-state-icon"]')
    expect(tile.classes()).toEqual(expect.arrayContaining(['rounded-full', 'bg-surface-hover']))
    expect(tile.find('[data-testid="icon"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="ui-empty-state-actions"]').find('[data-testid="action"]').exists()).toBe(true)
  })

  it('leaves out the icon tile and the actions row when their slots are empty', () => {
    const wrapper = mount(UiEmptyState, { props: { title: 'Nothing' } })

    expect(wrapper.find('[data-testid="ui-empty-state-icon"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-empty-state-actions"]').exists()).toBe(false)
  })
})
