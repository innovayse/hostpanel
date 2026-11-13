/**
 * Tests for ReportPage: title and description, the filters slot, the loading state that keeps the layout, the
 * refusal sentence from the store with its Retry button, and the "generated on" line after the
 * content.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, inject } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import ReportPage from '@/components/reports/ReportPage.vue'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'
import { useReportsStore } from '@/stores/reports'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the page with a filter and a body.
 *
 * @param retry - The screen's reload, when it offers one.
 * @returns The wrapper.
 */
const mountPage = (retry?: () => unknown) =>
  mount(ReportPage, {
    props: { title: 'Aging', description: 'Overdue invoices', retry },
    slots: { actions: '<p data-testid="actions-slot">actions</p>', filters: '<p data-testid="filter-slot">filters</p>', default: '<p data-testid="body-slot">body</p>' },
    global: { plugins: [createTestI18n(), createPinia()] },
  })

describe('ReportPage', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the title, description, filters, body and the generated-on line', () => {
    const wrapper = mountPage()

    expect(wrapper.text()).toContain('Aging')
    expect(wrapper.text()).toContain('Overdue invoices')
    expect(wrapper.find('[data-testid="actions-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="filter-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="body-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-timestamp"]').exists()).toBe(true)
  })

  it('keeps the header, filters and body while the store loads, marks it busy and hides the generated-on line', async () => {
    const wrapper = mountPage()
    useReportsStore().loading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="filter-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="body-slot"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="report-body"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="report-timestamp"]').exists()).toBe(false)
  })

  it('tells the parts in the body that the report is loading', async () => {
    const probe = defineComponent({
      setup: () => {
        const loading = inject(REPORT_LOADING, NOT_LOADING)
        return () => h('p', { 'data-testid': 'probe' }, String(loading.value))
      },
    })
    const wrapper = mount(ReportPage, {
      props: { title: 'Aging' },
      slots: { default: () => h(probe) },
      global: { plugins: [createTestI18n(), createPinia()] },
    })
    useReportsStore().loading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="probe"]').text()).toBe('true')
  })

  it('shows the sentence the store holds when a load was refused', async () => {
    const wrapper = mountPage()
    useReportsStore().error = 'No access.'
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="report-error"]').text()).toBe('No access.')
    expect(wrapper.find('[data-testid="body-slot"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-error-retry"]').exists()).toBe(false)
  })

  it('offers Retry on a refused load when the screen can reload', async () => {
    const retry = vi.fn()
    const wrapper = mountPage(retry)
    useReportsStore().error = 'No access.'
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="report-error"]').text()).toBe('No access.')
    await wrapper.get('[data-testid="report-error-retry"]').trigger('click')

    expect(retry).toHaveBeenCalledTimes(1)
  })
})
