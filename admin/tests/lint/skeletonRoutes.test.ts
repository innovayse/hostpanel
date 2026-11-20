/**
 * Guard: every routed screen can be drawn as a skeleton while its chunk downloads.
 *
 * Each lazy view or layout of the real router either has `meta.skeleton` (its own schema module, found by the
 * registry) or is named in `lib/skeletonExceptions.ts` with the reason its shape is unknown until it has loaded. The
 * list is checked too: an entry whose screen is no longer routed, or has since got a schema, fails, so it cannot rot.
 */
import { describe, expect, it } from 'vitest'
import type { RouteRecordRaw } from 'vue-router'
import { viewFileOf } from '@/lib/lazyRoute'
import { SKELETON_EXCEPTIONS } from '@/lib/skeletonExceptions'
import { skeletonSchemaOf } from '@/lib/skeletonRegistry'
import router from '@/router'

/** One route record of the real table with the file its loader stands for. */
interface RoutedScreen {
  /** The record's path as declared. */
  path: string
  /** The screen's file under `src/`, or undefined for a record with no tagged loader (a redirect, the shell). */
  file: string | undefined
  /** The record's meta. */
  meta: RouteRecordRaw['meta']
}

/**
 * Lists every record of a route table that has a component, nested records included.
 *
 * @param routes - Raw route records.
 * @returns The records with their loader's file.
 */
const screensOf = (routes: readonly RouteRecordRaw[]): RoutedScreen[] =>
  routes.flatMap((route) => [
    ...('component' in route && route.component !== undefined
      ? [{ path: route.path, file: viewFileOf(route.component), meta: route.meta }]
      : []),
    ...screensOf(route.children ?? []),
  ])

/** Every component record of the real router. */
const SCREENS = screensOf(router.options.routes)

/** The files of the lazy screens, once each. */
const FILES = [...new Set(SCREENS.flatMap((screen) => (screen.file === undefined ? [] : [screen.file])))]

/** The files the exception list names. */
const EXCEPTION_FILES = SKELETON_EXCEPTIONS.map((exception) => exception.file)

describe('skeleton routes', () => {
  it('reads the lazy screens of the router, layouts and report pages included', () => {
    expect(FILES).toContain('views/AddClientView.vue')
    expect(FILES).toContain('layouts/ClientLayout.vue')
    expect(FILES).toContain('views/DailyPerformanceView.vue')
  })

  it('leaves untagged only the eager shell layout', () => {
    const untagged = SCREENS.filter((screen) => screen.file === undefined)

    expect(untagged.map((screen) => screen.path)).toEqual(['/'])
    expect(untagged[0]?.meta?.shell).toBe(true)
  })

  it.each(FILES)('%s has a skeleton schema on its record, or is a named exception', (file) => {
    const records = SCREENS.filter((screen) => screen.file === file)

    if (EXCEPTION_FILES.includes(file)) {
      expect(records.filter((record) => record.meta?.skeleton !== undefined)).toEqual([])
    } else {
      expect(records.filter((record) => record.meta?.skeleton === undefined).map((record) => record.path)).toEqual([])
      expect(records.every((record) => record.meta?.skeleton === skeletonSchemaOf(file))).toBe(true)
    }
  })

  it.each(SKELETON_EXCEPTIONS.map((exception) => [exception.file, exception.reason] as const))(
    '%s is still routed and still has no schema (%s)',
    (file, reason) => {
      expect(reason).not.toBe('')
      expect(FILES).toContain(file)
      expect(skeletonSchemaOf(file)).toBeUndefined()
    },
  )

  it('names no exception twice', () => {
    expect(new Set(EXCEPTION_FILES).size).toBe(EXCEPTION_FILES.length)
  })
})
