/**
 * The shape gate for `tests/`: every component, store, api composable, composable, `lib/` module
 * and BFF route, middleware, plugin and utility has a mirror test at the same path under `tests/`
 * (`stores/auth.ts` -> `tests/stores/auth.test.ts`, `components/ui/UiCard.vue` ->
 * `tests/components/ui/UiCard.test.ts`).
 *
 * A source file that cannot have one yet is named in {@link KNOWN_GAPS} with the reason; the list
 * may only shrink: an entry whose mirror test now exists fails the stale-entry check below.
 *
 * @module tests/coverage-shape.test
 */

import { existsSync, readdirSync, statSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The client root. */
const ROOT = resolve(import.meta.dirname, '..')

/** Source folders whose files must be mirrored. */
const MIRRORED_AREAS = [
  'components',
  'stores',
  'composables',
  'lib',
  'server/api',
  'server/middleware',
  'server/plugins',
  'server/routes',
  'server/utils',
] as const

/**
 * Source files still without a mirror test, each with the reason. Remove the entry when the test
 * lands; the stale-entry check fails otherwise.
 */
const KNOWN_GAPS: Readonly<Record<string, string>> = {
  'components/ui/UiRowActions.vue': 'Added after the owner decision of 2026-11-27 that no new tests are written.',
}

/**
 * Lists every `.vue`/`.ts` file under a folder.
 *
 * @param dir Absolute folder path.
 * @param tests Whether to list test files (`*.test.ts`) instead of sources.
 * @returns Absolute file paths; empty when the folder does not exist.
 */
const walk = (dir: string, tests = false): string[] => {
  if (!existsSync(dir)) {
    return []
  }
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return walk(path, tests)
    }
    return /\.(vue|ts)$/.test(name) && name.endsWith('.test.ts') === tests ? [path] : []
  })
}

/**
 * The path of the test that mirrors a source file.
 *
 * @param source Source path relative to the client root, forward slashes.
 * @returns The mirror test path relative to the client root.
 */
const mirrorOf = (source: string): string => `tests/${source.replace(/\.(vue|ts)$/, '.test.ts')}`

/** Every mirrored source file, relative to the client root. */
const SOURCES = MIRRORED_AREAS
  .flatMap((area) => walk(join(ROOT, area)))
  .map((path) => relative(ROOT, path).split('\\').join('/'))
  .sort()

describe('tests/ mirrors the source tree', () => {
  it('finds the source files it is meant to check', () => {
    expect(SOURCES.length).toBeGreaterThan(100)
  })

  it('has a mirror test for every component, store, api composable, composable, lib and BFF server file', () => {
    const missing = SOURCES.filter((source) => !existsSync(join(ROOT, mirrorOf(source))) && !(source in KNOWN_GAPS))

    expect(missing).toEqual([])
  })

  it('keeps no stale entry in the known-gaps list', () => {
    const stale = Object.keys(KNOWN_GAPS).filter((source) => !SOURCES.includes(source) || existsSync(join(ROOT, mirrorOf(source))))

    expect(stale).toEqual([])
  })

  it('keeps every test under tests/, none beside its source', () => {
    const stray = MIRRORED_AREAS
      .flatMap((area) => walk(join(ROOT, area), true))
      .map((path) => relative(ROOT, path).split('\\').join('/'))

    expect(stray).toEqual([])
  })
})

