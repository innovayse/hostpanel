/**
 * Unit tests for UiRecordCard: the parts of a record card, its selected look and its loading placeholders.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiRecordCard from '@/components/ui/UiRecordCard.vue'

describe('UiRecordCard', () => {
  it('is a list item built on the card surface', () => {
    const wrapper = mount(UiRecordCard, { slots: { title: 'Ada' } })

    expect(wrapper.attributes('role')).toBe('listitem')
    expect(wrapper.classes()).toContain('glass')
  })

  it('draws the checkbox, heading, badge and actions on the first line and the subtitle under it', () => {
    const wrapper = mount(UiRecordCard, {
      slots: {
        select: '<i data-testid="select" />',
        title: 'Ada',
        badge: '<i data-testid="badge" />',
        actions: '<i data-testid="actions" />',
        subtitle: 'ada@x.io',
      },
    })

    expect(wrapper.get('.font-semibold').text()).toBe('Ada')
    expect(wrapper.findAll('.min-h-10 [data-testid]').map((part) => part.attributes('data-testid'))).toEqual(['select', 'badge', 'actions'])
    expect(wrapper.get('.text-text-muted').text()).toBe('ada@x.io')
  })

  it('drops the badge to a line of its own on a phone, so the title keeps the width beside the actions', () => {
    const wrapper = mount(UiRecordCard, {
      slots: { title: 'Ada', badge: '<i data-testid="badge" />', actions: '<i data-testid="actions" />' },
    })

    expect(wrapper.get('.min-h-10').classes()).toContain('max-sm:flex-wrap')
    expect(Array.from(wrapper.get('[data-testid="badge"]').element.parentElement?.classList ?? [])).toEqual(expect.arrayContaining(['max-sm:order-last', 'max-sm:basis-full']))
  })

  it('leaves out the parts it was given no slot for', () => {
    const wrapper = mount(UiRecordCard, { slots: { title: 'Ada' } })

    expect(wrapper.find('.text-text-muted').exists()).toBe(false)
    expect(wrapper.find('.flex-col > .flex-col[class~="gap-1.5"]').exists()).toBe(false)
  })

  it('draws the default slot as the body of field lines', () => {
    const wrapper = mount(UiRecordCard, { slots: { title: 'Ada', default: '<p data-testid="field">Email</p>' } })

    expect(wrapper.find('[data-testid="field"]').exists()).toBe(true)
  })

  it('marks a selected card with the accent ring', () => {
    expect(mount(UiRecordCard, { props: { selected: true }, slots: { title: 'Ada' } }).classes()).toContain('ring-1')
    expect(mount(UiRecordCard, { slots: { title: 'Ada' } }).classes()).not.toContain('ring-1')
  })

  it('draws placeholders shaped by the skeleton instead of the slots while loading', () => {
    const wrapper = mount(UiRecordCard, {
      props: { loading: true, skeleton: { select: true, subtitle: true, badge: true, actions: true, fields: 2 } },
      slots: { title: 'Ada' },
    })

    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.text()).not.toContain('Ada')
    expect(wrapper.find('[data-testid="ui-record-card-skeleton"]').exists()).toBe(true)
    // select, heading, badge, actions on the first line; subtitle; two fields of two bars.
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]').length).toBe(4 + 1 + 4)
  })
})
