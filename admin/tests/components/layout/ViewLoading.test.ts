/**
 * Mount tests for ViewLoading: a busy status region with skeletons and a screen-reader-only loading label.
 */
import { describe, expect, it } from 'vitest'
import ViewLoading from '@/components/layout/ViewLoading.vue'
import { mountWithApp } from '../../helpers/mount'

describe('ViewLoading', () => {
  it('announces a busy status region with skeletons', () => {
    const wrapper = mountWithApp(ViewLoading)
    const region = wrapper.get('[data-testid="view-loading"]')

    expect(region.attributes('role')).toBe('status')
    expect(region.attributes('aria-busy')).toBe('true')
    expect(region.findAll('[data-testid="ui-skeleton-bar"]').length).toBeGreaterThan(0)
  })

  it('sketches a title, a row of cards and a table card', () => {
    const region = mountWithApp(ViewLoading).get('[data-testid="view-loading"]')

    expect(region.findAll('[data-testid="ui-skeleton-card"]')).toHaveLength(3)
    expect(region.findAll('[data-testid="ui-skeleton-table-row"]')).toHaveLength(5)
  })

  it('names itself to screen readers as loading', () => {
    const wrapper = mountWithApp(ViewLoading)

    expect(wrapper.text()).toContain('Loading')
  })
})
