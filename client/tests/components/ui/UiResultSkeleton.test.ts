/**
 * Unit tests for UiResultSkeleton: it repeats the shape of a result row (name, price, action),
 * draws every part with the kit skeleton, and announces one loading status.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiResultSkeleton from '~/components/ui/UiResultSkeleton.vue'

describe('UiResultSkeleton', () => {
  it('draws three rows by default and as many as asked', () => {
    expect(mountWithKit(UiResultSkeleton).findAll('[data-testid="ui-result-skeleton-row"]')).toHaveLength(3)
    expect(mountWithKit(UiResultSkeleton, { props: { rows: 5 } }).findAll('[data-testid="ui-result-skeleton-row"]')).toHaveLength(5)
  })

  it('gives every row a name, a price and an action drawn with the kit skeleton', () => {
    const rows = mountWithKit(UiResultSkeleton).findAll('[data-testid="ui-result-skeleton-row"]')

    for (const row of rows) {
      for (const part of ['name', 'price', 'action']) {
        expect(row.find(`[data-testid="ui-result-skeleton-${part}"] [data-testid="ui-skeleton"]`).exists()).toBe(true)
      }
    }
  })

  it('makes the name stop short and the action a block, not three full-width bars', () => {
    const row = mountWithKit(UiResultSkeleton).find('[data-testid="ui-result-skeleton-row"]')

    expect(row.find('[data-testid="ui-result-skeleton-name"] [data-testid="ui-skeleton"]').classes()).toContain('w-3/4')
    expect(row.find('[data-testid="ui-result-skeleton-action"] [data-testid="ui-skeleton"]').classes()).toEqual(expect.arrayContaining(['rounded-xl', 'h-10']))
  })

  it('announces one loading status and hides the rows from assistive tech', () => {
    const wrapper = mountWithKit(UiResultSkeleton)

    expect(wrapper.attributes('role')).toBe('status')
    expect(wrapper.attributes('aria-label')).toBe('ui.loading')
    expect(wrapper.find('[data-testid="ui-result-skeleton-row"]').attributes('aria-hidden')).toBe('true')
  })

  it('lets a caller replace the test id of the group', () => {
    expect(mountWithKit(UiResultSkeleton, { attrs: { 'data-testid': 'pending' } }).attributes('data-testid')).toBe('pending')
  })
})
