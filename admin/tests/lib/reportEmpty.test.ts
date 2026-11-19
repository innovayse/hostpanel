/**
 * Unit tests for the report empty rule: the page scope collapses only when parts exist, all are
 * empty and the report is not loading; a part registers and leaves with its component; the chart
 * emptiness check; the sentence of the empty state the page carries for its parts.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, provide, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { REPORT_EMPTY, createReportEmptyScope, isReportChartEmpty, useReportEmptyText, useReportPartEmpty } from '@/lib/reportEmpty'

describe('createReportEmptyScope', () => {
  it('is not collapsed without parts', () => {
    expect(createReportEmptyScope(ref(false), ref(undefined)).collapsed.value).toBe(false)
  })

  it('collapses when every part is empty and not before', () => {
    /** The page scope under test. */
    const scope = createReportEmptyScope(ref(false), ref(undefined))
    /** The chart part. */
    const chart = Symbol('chart')
    /** The table part. */
    const table = Symbol('table')

    scope.set(chart, true)
    scope.set(table, false)
    expect(scope.collapsed.value).toBe(false)

    scope.set(table, true)
    expect(scope.collapsed.value).toBe(true)
  })

  it('stops counting a part that left', () => {
    /** The page scope under test. */
    const scope = createReportEmptyScope(ref(false), ref(undefined))
    /** The chart part. */
    const chart = Symbol('chart')
    /** The table part. */
    const table = Symbol('table')
    scope.set(chart, true)
    scope.set(table, false)

    scope.remove(table)

    expect(scope.collapsed.value).toBe(true)
  })

  it('never collapses while the report loads', () => {
    /** Whether the report loads. */
    const busy = ref(true)
    /** The page scope under test. */
    const scope = createReportEmptyScope(busy, ref(undefined))
    scope.set(Symbol('chart'), true)

    expect(scope.collapsed.value).toBe(false)
    busy.value = false
    expect(scope.collapsed.value).toBe(true)
  })
})

describe('useReportPartEmpty', () => {
  it('registers the part, follows its flag and forgets it on unmount', async () => {
    /** Whether the part has nothing to show. */
    const empty = ref(true)
    /** The scope the page provides. */
    const scope = createReportEmptyScope(ref(false), ref(undefined))
    /** A part that registers with the page and prints whether the page collapsed. */
    const Part = defineComponent({
      setup: () => {
        const collapsed = useReportPartEmpty(empty)
        return () => h('i', String(collapsed.value))
      },
    })
    /** A page that provides the scope. */
    const Page = defineComponent({
      setup: () => {
        provide(REPORT_EMPTY, scope)
        return () => h(Part)
      },
    })

    const wrapper = mount(Page)
    await wrapper.vm.$nextTick()
    expect(scope.collapsed.value).toBe(true)
    expect(wrapper.text()).toBe('true')

    empty.value = false
    await wrapper.vm.$nextTick()
    expect(scope.collapsed.value).toBe(false)

    empty.value = true
    await wrapper.vm.$nextTick()
    expect(scope.collapsed.value).toBe(true)

    wrapper.unmount()
    expect(scope.collapsed.value).toBe(false)
  })

  it('reads false outside a page', () => {
    /** A part with no page above it. */
    const Part = defineComponent({
      setup: () => {
        const collapsed = useReportPartEmpty(ref(true))
        return () => h('i', String(collapsed.value))
      },
    })

    expect(mount(Part).text()).toBe('false')
  })
})

describe('useReportEmptyText', () => {
  it('reads the sentence the page carries, and follows it', () => {
    /** The sentence the page carries. */
    const text = ref<string | undefined>('No invoices.')
    /** The scope the page provides. */
    const scope = createReportEmptyScope(ref(false), text)
    /** A part below a page that prints the page sentence. */
    const Part = defineComponent({
      setup: () => {
        const sentence = useReportEmptyText()
        return () => h('i', String(sentence.value))
      },
    })
    /** A page that provides the scope. */
    const Page = defineComponent({
      setup: () => {
        provide(REPORT_EMPTY, scope)
        return () => h(Part)
      },
    })

    const wrapper = mount(Page)
    expect(wrapper.text()).toBe('No invoices.')
  })

  it('reads nothing outside a page, so the generic sentence shows', () => {
    /** A part with no page above it. */
    const Part = defineComponent({
      setup: () => {
        const sentence = useReportEmptyText()
        return () => h('i', String(sentence.value))
      },
    })

    expect(mount(Part).text()).toBe('undefined')
  })
})

describe('isReportChartEmpty', () => {
  it('is empty without labels, without data or with only zeros', () => {
    expect(isReportChartEmpty([], [{ label: 'A', data: [] }])).toBe(true)
    expect(isReportChartEmpty(['Jan'], [{ label: 'A', data: [] }])).toBe(true)
    expect(isReportChartEmpty(['Jan', 'Feb'], [{ label: 'A', data: [0, 0] }])).toBe(true)
  })

  it('has data once any value of any series is non-zero', () => {
    expect(isReportChartEmpty(['Jan', 'Feb'], [{ label: 'A', data: [0, 0] }, { label: 'B', data: [0, 4] }])).toBe(false)
  })
})
