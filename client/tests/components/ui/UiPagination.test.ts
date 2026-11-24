/**
 * Unit tests for UiPagination: the page window, the previous and next buttons at the edges, page
 * selection and the page-size select resetting to page 1.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiPagination from '~/components/ui/UiPagination.vue'

describe('UiPagination', () => {
  it('shows a window of five pages around the current page', () => {
    const wrapper = mountWithKit(UiPagination, { props: { modelValue: 5, totalPages: 10, perPage: 10 } })

    const pages = wrapper.findAll('[data-testid^="ui-pagination-page-"]').map((button) => button.text())
    expect(pages).toEqual(['3', '4', '5', '6', '7'])
    expect(wrapper.find('[data-testid="ui-pagination-page-5"]').attributes('aria-current')).toBe('page')
  })

  it('clamps the window to the first and last pages', () => {
    const wrapper = mountWithKit(UiPagination, { props: { modelValue: 1, totalPages: 2, perPage: 10 } })

    const pages = wrapper.findAll('[data-testid^="ui-pagination-page-"]').map((button) => button.text())
    expect(pages).toEqual(['1', '2'])
  })

  it('disables previous on the first page and next on the last', () => {
    const first = mountWithKit(UiPagination, { props: { modelValue: 1, totalPages: 3, perPage: 10 } })
    const last = mountWithKit(UiPagination, { props: { modelValue: 3, totalPages: 3, perPage: 10 } })

    expect(first.find('[data-testid="ui-pagination-prev"]').attributes('disabled')).toBeDefined()
    expect(first.find('[data-testid="ui-pagination-next"]').attributes('disabled')).toBeUndefined()
    expect(last.find('[data-testid="ui-pagination-next"]').attributes('disabled')).toBeDefined()
  })

  it('emits the next, previous and chosen page', async () => {
    const wrapper = mountWithKit(UiPagination, { props: { modelValue: 2, totalPages: 5, perPage: 10 } })

    await wrapper.find('[data-testid="ui-pagination-next"]').trigger('click')
    await wrapper.find('[data-testid="ui-pagination-prev"]').trigger('click')
    await wrapper.find('[data-testid="ui-pagination-page-4"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[3], [1], [4]])
  })

  it('emits the new page size and returns to page 1', async () => {
    const wrapper = mountWithKit(UiPagination, { props: { modelValue: 3, totalPages: 5, perPage: 10 } })

    await wrapper.find('[data-testid="ui-pagination-size"]').trigger('click')
    await wrapper.find('[data-testid="ui-select-option-25"]').trigger('click')

    expect(wrapper.emitted('update:perPage')?.[0]).toEqual([25])
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([1])
  })

  it('offers the page sizes it is given', async () => {
    const wrapper = mountWithKit(UiPagination, { props: { modelValue: 1, totalPages: 1, perPage: 5, pageSizes: [5, 20] } })

    await wrapper.find('[data-testid="ui-pagination-size"]').trigger('click')

    expect(wrapper.findAll('[role="option"]').map((option) => option.text())).toEqual(['5', '20'])
  })

  it('paints the current page solid primary and gives every button a focus ring', () => {
    const wrapper = mountWithKit(UiPagination, { props: { modelValue: 2, totalPages: 3, perPage: 10 } })
    const current = wrapper.find('[data-testid="ui-pagination-page-2"]')
    const other = wrapper.find('[data-testid="ui-pagination-page-1"]')

    expect(current.classes()).toEqual(expect.arrayContaining(['bg-primary-600', 'text-on-primary']))
    expect(other.classes()).not.toContain('bg-primary-600')
    expect(other.classes()).toContain('focus-visible:ring-2')
    expect(wrapper.find('[data-testid="ui-pagination-next"]').classes()).toContain('focus-visible:ring-2')
  })

  it('wraps its buttons and keeps 44px touch targets on a phone', () => {
    const wrapper = mountWithKit(UiPagination, { props: { modelValue: 1, totalPages: 3, perPage: 10 } })

    expect(wrapper.find('nav').classes()).toContain('flex-wrap')
    expect(wrapper.find('[data-testid="ui-pagination-page-1"]').classes()).toEqual(expect.arrayContaining(['h-11', 'w-11']))
    expect(wrapper.find('[data-testid="ui-pagination-next"]').classes()).toContain('min-h-11')
  })
})
