/**
 * Guard: no admin test replaces one of our own modules with `vi.mock` or `vi.doMock`. A store, an api
 * composable, a composable or a helper of ours runs for real in every test; only what we do not own is
 * substituted, and the network is stubbed below `useApi` through the fetch and route helpers. The client
 * has the same guard for its tests in `client/tests/config/noOwnMocks.test.ts` (it also covers a composable
 * faked as a global): the two apps cannot read each other's files, so each reads its own.
 * This test reads every file under `admin/tests` and fails as soon as a mock of one of our own modules
 * comes back.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The admin's test folder. */
const TESTS_ROOT = join(__dirname, '..')

/** This file: it quotes the calls it hunts for, so it is the one file the scan skips. */
const THIS_FILE = __filename

/** A `vi.mock` or `vi.doMock` call with a literal module specifier, captured in group 2. */
const MOCK_CALL = /\bvi\.(?:mock|doMock)\(\s*(['"`])([^'"`]+)\1/g

/** The start of a specifier that names a module of ours: an alias, a relative path or an import map entry. */
const OWN_MODULE = /^(?:~|@\/|\.|#)/

/**
 * Lists every `.ts` file under a folder, skipping dependencies.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the files found.
 */
const tsFiles = (dir: string): string[] => {
  if (!existsSync(dir)) {
    return []
  }
  return readdirSync(dir).flatMap((name) => {
    if (name === 'node_modules' || name.startsWith('.')) {
      return []
    }
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return tsFiles(path)
    }
    return name.endsWith('.ts') ? [path] : []
  })
}

/**
 * Finds the mocks of our own modules in one file's text.
 *
 * @param text - File contents.
 * @returns The module specifiers mocked, empty when the file mocks nothing of ours.
 */
const ownMocks = (text: string): string[] =>
  [...text.matchAll(MOCK_CALL)].map((match) => match[2] ?? '').filter((specifier) => OWN_MODULE.test(specifier))

/** Every test file the guard reads. */
const SOURCES: readonly string[] = tsFiles(TESTS_ROOT).filter((path) => path !== THIS_FILE)

describe('no mocks of our own modules in the admin tests', () => {
  it('reads the admin tests', () => {
    expect(SOURCES.length).toBeGreaterThan(0)
  })

  it.each(SOURCES.map((path) => [relative(TESTS_ROOT, path), path]))('%s mocks no module of ours', (_name, path) => {
    expect(ownMocks(readFileSync(path, 'utf8'))).toEqual([])
  })

  it('catches a store, a composable and a relative module being mocked', () => {
    expect(ownMocks(`vi.mock('@/stores/orders', () => ({}))`)).toEqual(['@/stores/orders'])
    expect(ownMocks(`vi.mock("@/composables/useApi", () => ({}))`)).toEqual(['@/composables/useApi'])
    expect(ownMocks(`vi.doMock('../src/lib/money', () => ({}))`)).toEqual(['../src/lib/money'])
    expect(ownMocks(`vi.mock(\n  '#imports',\n  () => ({}))`)).toEqual(['#imports'])
  })

  it('leaves the mocks of packages we do not own alone', () => {
    expect(ownMocks(`vi.mock('chart.js', () => ({}))`)).toEqual([])
    expect(ownMocks(`vi.mock('@vuepic/vue-datepicker', () => ({}))`)).toEqual([])
    expect(ownMocks(`vi.mock('vitepress', () => ({}))`)).toEqual([])
  })
})
