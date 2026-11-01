/**
 * Unit tests for UiFilterCard: card surface with the four slots in order.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'

describe('UiFilterCard', () => {
  it('renders fields, default, dates and actions in that order', () => {
    const wrapper = mount(UiFilterCard, {
      slots: { fields: 'FIELDS', default: 'MIDDLE', dates: 'DATES', actions: 'ACTIONS' },
    })
    const text = wrapper.text()

    expect(text.indexOf('FIELDS')).toBeLessThan(text.indexOf('MIDDLE'))
    expect(text.indexOf('MIDDLE')).toBeLessThan(text.indexOf('DATES'))
    expect(text.indexOf('DATES')).toBeLessThan(text.indexOf('ACTIONS'))
  })

  it('is a bordered card and keeps caller classes', () => {
    const wrapper = mount(UiFilterCard, { attrs: { class: 'mt-2', 'data-testid': 'filters' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['border', 'bg-surface-card', 'mt-2']))
    expect(wrapper.attributes('data-testid')).toBe('filters')
  })

  it('is a size container so a grid inside follows the card width', () => {
    expect(mount(UiFilterCard).classes()).toContain('@container')
  })
})
