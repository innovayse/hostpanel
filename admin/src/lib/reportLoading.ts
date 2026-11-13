/**
 * Injection key that tells the parts of a report screen whether its report is loading.
 *
 * `ReportPage` provides it from the reports store; the stat tiles, charts and tables read it and
 * draw skeletons of their own layout, so the screen keeps its shape while the data arrives.
 */
import { ref } from 'vue'
import type { InjectionKey, Ref } from 'vue'

/** Carries the loading flag from `ReportPage` down to the report parts. */
export const REPORT_LOADING: InjectionKey<Readonly<Ref<boolean>>> = Symbol('report-loading')

/** What a report part reads when it stands outside a `ReportPage`: never loading. */
export const NOT_LOADING: Readonly<Ref<boolean>> = ref(false)
