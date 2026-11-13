/**
 * Unit tests for UiPagination: window of pages, navigation and edge states.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiPagination from '@/components/ui/UiPagination.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the pagination with the English catalogue.
 *
 * @param props - Props for the component.
 * @returns The wrapper.
 */
const mountPagination = (props: { page: number; total: number; pageSize: number; pageSizeOptions?: number[]; testid?: string }) =>
  mount(UiPagination, { props, global: { plugins: [createTestI18n()] } })

describe('UiPagination', () => {
  it('keeps its layout while loading, with a bar for the range and every control disabled', () => {
    const wrapper = mount(UiPagination, {
      props: { page: 1, total: 0, pageSize: 10, pageSizeOptions: [10, 20], loading: true },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.find('nav').exists()).toBe(true)
    const range = wrapper.get('[data-testid="pagination-range"]')
    expect(range.text()).toBe('')
    expect(range.attributes('aria-hidden')).toBe('true')
    expect(range.classes()).toContain('bg-surface-hover')
    expect(wrapper.get('[data-testid="pagination-prev"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="pagination-next"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="pagination-page-1"]').attributes('disabled')).toBeDefined()
  })

  it('renders nothing for a single page', () => {
    expect(mountPagination({ page: 1, total: 10, pageSize: 10 }).find('nav').exists()).toBe(false)
  })

  it('shows every page of a short list and marks the current one', () => {
    const wrapper = mountPagination({ page: 2, total: 30, pageSize: 10 })

    expect(wrapper.find('[data-testid="pagination-page-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="pagination-page-3"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="pagination-page-2"]').attributes('aria-current')).toBe('page')
  })

  it('collapses far pages into a gap around the current page', () => {
    const wrapper = mountPagination({ page: 5, total: 100, pageSize: 10 })
    const numbers = wrapper.findAll('[data-testid^="pagination-page-"]').map(b => b.text())

    expect(numbers).toEqual(['1', '4', '5', '6', '10'])
    expect(wrapper.text()).toContain('…')
  })

  it('goes to the next and previous page', async () => {
    const wrapper = mountPagination({ page: 2, total: 50, pageSize: 10 })

    await wrapper.get('[data-testid="pagination-next"]').trigger('click')
    await wrapper.get('[data-testid="pagination-prev"]').trigger('click')

    expect(wrapper.emitted('update:page')).toEqual([[3], [1]])
  })

  it('goes to a clicked page but not to the current one', async () => {
    const wrapper = mountPagination({ page: 2, total: 50, pageSize: 10 })

    await wrapper.get('[data-testid="pagination-page-3"]').trigger('click')
    await wrapper.get('[data-testid="pagination-page-2"]').trigger('click')

    expect(wrapper.emitted('update:page')).toEqual([[3]])
  })

  it('disables previous on the first page and next on the last', () => {
    const first = mountPagination({ page: 1, total: 30, pageSize: 10 })
    const last = mountPagination({ page: 3, total: 30, pageSize: 10 })

    expect(first.get('[data-testid="pagination-prev"]').attributes('disabled')).toBeDefined()
    expect(last.get('[data-testid="pagination-next"]').attributes('disabled')).toBeDefined()
  })

  it('prefixes its test ids', () => {
    const wrapper = mountPagination({ page: 1, total: 30, pageSize: 10, testid: 'orders' })

    expect(wrapper.find('[data-testid="orders-next"]').exists()).toBe(true)
  })

  it('names the arrow buttons for assistive technology', () => {
    const wrapper = mountPagination({ page: 2, total: 50, pageSize: 10 })

    expect(wrapper.get('[data-testid="pagination-prev"]').attributes('aria-label')).toBe('Previous')
    expect(wrapper.get('[data-testid="pagination-next"]').attributes('aria-label')).toBe('Next')
  })

  it('states the range of rows on the page', () => {
    expect(mountPagination({ page: 2, total: 25, pageSize: 10 }).get('[data-testid="pagination-range"]').text()).toBe('11 to 20 of 25')
    expect(mountPagination({ page: 3, total: 25, pageSize: 10 }).get('[data-testid="pagination-range"]').text()).toBe('21 to 25 of 25')
  })

  it('shows a page caption for phones next to the numbers', () => {
    expect(mountPagination({ page: 2, total: 50, pageSize: 10 }).text()).toContain('Page 2 of 5')
  })

  it('shows a lone hidden page instead of a gap', () => {
    const wrapper = mountPagination({ page: 4, total: 70, pageSize: 10 })
    const numbers = wrapper.findAll('[data-testid^="pagination-page-"]').map(b => b.text())

    expect(numbers).toEqual(['1', '2', '3', '4', '5', '6', '7'])
    expect(wrapper.text()).not.toContain('…')
  })

  describe('rows per page', () => {
    it('has no select unless sizes are offered', () => {
      expect(mountPagination({ page: 1, total: 30, pageSize: 10 }).find('[data-testid="pagination-page-size"]').exists()).toBe(false)
    })

    it('reports the size the user picks', async () => {
      const wrapper = mountPagination({ page: 1, total: 100, pageSize: 10, pageSizeOptions: [10, 25, 50] })

      await wrapper.get('[data-testid="pagination-page-size"] button').trigger('click')
      const option = wrapper.findAll('[data-testid="pagination-page-size"] [role="option"]').find(o => o.text() === '25')
      await option?.trigger('mousedown')

      expect(wrapper.emitted('update:pageSize')).toEqual([[25]])
    })

    it('stays for a single page that is longer than the smallest size, and goes when it is not', () => {
      const long = mountPagination({ page: 1, total: 30, pageSize: 50, pageSizeOptions: [10, 25, 50] })
      const short = mountPagination({ page: 1, total: 8, pageSize: 10, pageSizeOptions: [10, 25, 50] })

      expect(long.find('[data-testid="pagination-page-size"]').exists()).toBe(true)
      expect(short.find('[data-testid="pagination-range"]').exists()).toBe(false)
    })
  })
})
