/**
 * The one "this report has no data" rule of the report screens.
 *
 * `ReportPage` provides a {@link ReportEmptyScope}; every `ReportTable` and `ReportChartCard`
 * below it reports through {@link useReportPartEmpty} whether it has data. When the page has at
 * least one such part and all of them are empty, the parts draw nothing and the page draws a single
 * compact empty state for the whole report (stats and filters stay). A part that is empty while a
 * sibling has data draws the same compact state in its own place, and a part outside a page does
 * the same, so no screen ever shows an empty chart card, a bare table header or a sideways scrollbar.
 *
 * The sentence of every one of those states is the page's: the scope carries it (from the report's schema, or the one the
 * screen words itself), so a table or a chart never repeats the text its report already has.
 */
import { computed, inject, onUnmounted, reactive, watchEffect } from 'vue'
import type { ComputedRef, InjectionKey, Ref } from 'vue'
import type { UiChartSeries } from '@/utils/ui/chart'

/** What `ReportPage` gives the parts of a report: where they register and whether the page shows the one empty state. */
export interface ReportEmptyScope {
  /** Records whether one part is empty. */
  set: (id: symbol, empty: boolean) => void
  /** Forgets a part that left the page. */
  remove: (id: symbol) => void
  /** True when the page has parts and every one of them is empty: the parts then draw nothing. */
  collapsed: ComputedRef<boolean>
  /** The sentence of the empty state, already translated; absent when the report has none of its own (the generic one shows). */
  text: Readonly<Ref<string | undefined>>
}

/** Carries the empty scope from `ReportPage` down to the report parts. */
export const REPORT_EMPTY: InjectionKey<ReportEmptyScope> = Symbol('report-empty')

/**
 * Creates the scope a `ReportPage` provides.
 *
 * @param busy - Whether the report loads or refreshes; a loading report is never empty, its parts draw skeletons.
 * @param text - The sentence of the empty state, already translated.
 * @returns The scope: registration functions, the collapsed flag and the sentence.
 */
export const createReportEmptyScope = (busy: Readonly<Ref<boolean>>, text: Readonly<Ref<string | undefined>>): ReportEmptyScope => {
  /** Empty flag of every registered part. */
  const parts = reactive(new Map<symbol, boolean>())

  return {
    /**
     * Records whether one part is empty.
     *
     * @param id - The part's own symbol.
     * @param empty - True while the part has nothing to show.
     */
    set: (id, empty) => {
      parts.set(id, empty)
    },
    /**
     * Forgets a part that left the page.
     *
     * @param id - The part's own symbol.
     */
    remove: (id) => {
      parts.delete(id)
    },
    collapsed: computed<boolean>(() => !busy.value && parts.size > 0 && [...parts.values()].every((empty) => empty)),
    text,
  }
}

/**
 * Reads the sentence of the empty state of the page above.
 *
 * @returns The page's sentence; undefined outside a page or when the report has none of its own, so the generic one shows.
 */
export const useReportEmptyText = (): ComputedRef<string | undefined> => {
  const scope = inject(REPORT_EMPTY, null)
  return computed<string | undefined>(() => scope?.text.value)
}

/**
 * Registers one report part with the page above it.
 *
 * @param empty - Whether this part has no data.
 * @returns True when the page shows the single empty state, so this part should draw nothing; always false outside a page.
 */
export const useReportPartEmpty = (empty: Readonly<Ref<boolean>>): ComputedRef<boolean> => {
  const scope = inject(REPORT_EMPTY, null)
  if (scope !== null) {
    const id = Symbol('report-part')
    watchEffect(() => scope.set(id, empty.value))
    onUnmounted(() => scope.remove(id))
  }
  return computed<boolean>(() => scope?.collapsed.value ?? false)
}

/**
 * Whether a chart has nothing worth plotting: no labels, or every value of every series missing or zero.
 *
 * @param labels - Category labels of the chart.
 * @param series - Data series of the chart.
 * @returns True when the chart would draw an empty plot.
 */
export const isReportChartEmpty = (labels: readonly string[], series: readonly UiChartSeries[]): boolean =>
  labels.length === 0 || series.every((entry) => entry.data.every((value) => !value))
