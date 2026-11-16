/**
 * Mount tests for ViewLoading: a busy status region built from the kit loading states and a screen-reader-only loading label.
 */
import { describe, expect, it } from 'vitest'
import ViewLoading from '@/components/layout/ViewLoading.vue'
import { mountWithApp } from '../../helpers/mount'

describe('ViewLoading', () => {
  it('announces a busy status region with placeholder bars', () => {
    const wrapper = mountWithApp(ViewLoading)
    const region = wrapper.get('[data-testid="view-loading"]')

    expect(region.attributes('role')).toBe('status')
    expect(region.attributes('aria-busy')).toBe('true')
    expect(region.findAll('[data-testid="ui-skeleton-bar"]').length).toBeGreaterThan(0)
  })

  it('draws a loading page header and a loading card, and no skeleton preset', () => {
    const region = mountWithApp(ViewLoading).get('[data-testid="view-loading"]')

    expect(region.find('[data-testid="ui-page-header-skeleton"]').exists()).toBe(true)
    expect(region.find('[data-testid="ui-card-skeleton"]').exists()).toBe(true)
    expect(region.find('[data-testid="ui-skeleton-card"]').exists()).toBe(false)
    expect(region.find('[data-testid="ui-skeleton-table-row"]').exists()).toBe(false)
  })

  it('names itself to screen readers as loading', () => {
    const wrapper = mountWithApp(ViewLoading)

    expect(wrapper.text()).toContain('Loading')
  })
})
