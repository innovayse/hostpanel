/**
 * Unit tests for the report loading injection key and its default: a part outside a report page
 * reads "not loading", and a page's provided flag reaches the parts below it.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, inject, provide, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { NOT_LOADING, REPORT_LOADING } from '@/lib/reportLoading'

describe('report loading', () => {
  it('is a flag that reads false by default', () => {
    expect(NOT_LOADING.value).toBe(false)
  })

  it('reaches a part below the page that provides it, and falls back outside one', () => {
    const loading = ref(true)
    const Part = defineComponent({
      setup: () => {
        const flag = inject(REPORT_LOADING, NOT_LOADING)
        return () => h('span', { 'data-testid': 'part' }, String(flag.value))
      },
    })
    const Page = defineComponent({
      setup: () => {
        provide(REPORT_LOADING, loading)
        return () => h(Part)
      },
    })

    expect(mount(Page).get('[data-testid="part"]').text()).toBe('true')
    expect(mount(Part).get('[data-testid="part"]').text()).toBe('false')
  })
})
