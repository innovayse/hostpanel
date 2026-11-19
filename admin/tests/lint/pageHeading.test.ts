/**
 * Guard: every routed admin screen draws a page heading (`<h1>`), through the shared header path.
 *
 * A screen's `<h1>` is the `UiPageHeader` title (a layout may own it instead, as the client layout does for the client's
 * name). The sweep follows each routed screen, with the layouts it is nested in, through every `.vue` file it imports and
 * asks whether one of them draws the heading: `<UiPageHeader`, or the one `UiHeading` of level 1 a layout or the auth shell
 * owns (the sign-in, setup, verify, forbidden and not-found pages reach it through `AuthShell`, so none needs an
 * exception). A screen that reaches none fails, and so does a hand-rolled `<h1>` element, which the kit's heading replaces.
 */
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import type { RouteRecordRaw } from 'vue-router'
import { viewFileOf } from '@/lib/lazyRoute'
import router from '@/router'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** What a source must contain to draw the page heading: the shared header, or a kit heading of level 1. */
const HEADING_MARKERS: readonly RegExp[] = [/<UiPageHeader[\s>]/, /<UiHeading[^>]*:level="1"/]

/** A hand-rolled `<h1>` element. */
const RAW_H1 = /<h1[\s>]/

/** One routed screen with the layouts it is nested in. */
interface RoutedScreen {
  /** The screen's file under `src/`. */
  file: string
  /** The files of the layouts that wrap it, outermost first. */
  layouts: string[]
}

/**
 * Lists every tagged screen of a route table with the layouts above it.
 *
 * @param routes - Raw route records.
 * @param layouts - The tagged layout files above these records.
 * @returns One entry per record that has a component.
 */
const screensOf = (routes: readonly RouteRecordRaw[], layouts: string[] = []): RoutedScreen[] =>
  routes.flatMap((route) => {
    const file = 'component' in route && route.component !== undefined ? viewFileOf(route.component) : undefined
    const below = file === undefined ? layouts : [...layouts, file]
    const children = screensOf(route.children ?? [], below)

    return file !== undefined && (route.children ?? []).length === 0 ? [{ file, layouts }, ...children] : children
  })

/**
 * Reads a file under `src/`.
 *
 * @param file - Path under `src/`.
 * @returns The source text.
 */
const sourceOf = (file: string): string => readFileSync(join(SRC, file), 'utf8')

/**
 * Lists the `.vue` files a source imports, statically or lazily, that exist under `src/`.
 *
 * @param source - The file's text.
 * @returns Their paths under `src/`.
 */
const importsOf = (source: string): string[] =>
  [...source.matchAll(/from '@\/([^']+\.vue)'|import\('@\/([^']+\.vue)'\)/g)]
    .map((match) => match[1] ?? match[2] ?? '')
    .filter((file) => file !== '' && existsSync(join(SRC, file)))

/**
 * Tells whether a file, or any file it imports, draws the page heading.
 *
 * @param file - Path under `src/`.
 * @param seen - Files already visited, so a cycle ends.
 * @returns True when a heading marker is reached.
 */
const drawsHeading = (file: string, seen: Set<string> = new Set()): boolean => {
  if (seen.has(file)) {
    return false
  }
  seen.add(file)
  const source = sourceOf(file)

  return HEADING_MARKERS.some((marker) => marker.test(source)) || importsOf(source).some((next) => drawsHeading(next, seen))
}

/** Every routed screen, once each with its layouts. */
const SCREENS = screensOf(router.options.routes)

describe('page heading', () => {
  it('reads the routed screens', () => {
    expect(SCREENS.map((screen) => screen.file)).toContain('views/FaqFormView.vue')
    expect(SCREENS.map((screen) => screen.file)).toContain('views/SlideFormView.vue')
  })

  it.each(SCREENS.map((screen) => [screen.file, screen] as const))('%s draws a page heading', (_file, screen) => {
    expect([screen.file, ...screen.layouts].some((file) => drawsHeading(file))).toBe(true)
  })

  it('draws no hand-rolled h1 element', () => {
    const offenders = SCREENS.flatMap((screen) => [screen.file, ...screen.layouts]).filter((file) => RAW_H1.test(sourceOf(file)))

    expect([...new Set(offenders)]).toEqual([])
  })
})
