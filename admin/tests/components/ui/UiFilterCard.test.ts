/**
 * Unit tests for UiFilterCard: the card frame, the row shape and the block shape.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'

describe('UiFilterCard', () => {
  it('renders fields, their actions, default and dates in that order', () => {
    const wrapper = mount(UiFilterCard, {
      slots: { fields: 'FIELDS', default: 'MIDDLE', dates: 'DATES', actions: 'ACTIONS' },
    })
    const text = wrapper.text()

    expect(text.indexOf('FIELDS')).toBeLessThan(text.indexOf('ACTIONS'))
    expect(text.indexOf('ACTIONS')).toBeLessThan(text.indexOf('MIDDLE'))
    expect(text.indexOf('MIDDLE')).toBeLessThan(text.indexOf('DATES'))
  })

  it('puts the actions on the fields row, bottom-aligned, full width on phones', () => {
    const wrapper = mount(UiFilterCard, { slots: { fields: 'FIELDS', actions: 'ACTIONS' } })
    const row = wrapper.get('[data-testid="ui-filter-card-row"]')
    const actions = row.get('[data-testid="ui-filter-card-actions"]')

    expect(row.classes()).toEqual(expect.arrayContaining(['flex-row', 'items-end', 'flex-wrap']))
    expect(actions.classes()).toEqual(expect.arrayContaining(['max-sm:w-full']))
  })

  it('makes the actions the footer of a block of fields, right-aligned', () => {
    const wrapper = mount(UiFilterCard, { slots: { default: 'GRID', actions: 'ACTIONS' } })
    const actions = wrapper.get('[data-testid="ui-filter-card-actions"]')

    expect(wrapper.find('[data-testid="ui-filter-card-row"]').exists()).toBe(false)
    expect(actions.classes()).toContain('justify-end')
  })

  it('is a frosted card and keeps caller classes', () => {
    const wrapper = mount(UiFilterCard, { attrs: { class: 'mt-2', 'data-testid': 'filters' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['glass', 'rounded-card', 'mt-2']))
    expect(wrapper.attributes('data-testid')).toBe('filters')
  })
})
