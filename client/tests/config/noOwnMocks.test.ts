/**
 * Guard: no client test replaces one of our own modules. A store, an api composable, a composable or a
 * helper of ours runs for real in every test; only what we do not own is substituted, and the network is
 * stubbed below `$fetch` through the stub-server helpers. The admin has the same guard for its tests in
 * `admin/tests/lint/noOwnMocks.test.ts`: the two apps cannot read each other's files, so each reads its own.
 *
 * Two forms of replacing are caught. `vi.mock` and `vi.doMock` of a module of ours are refused outright.
 * `vi.stubGlobal` of one of our own auto-imported composables or stores is the same replacement by
 * another API (a Nuxt auto-import is a global under test), and it is refused too, except in the files of
 * {@link KNOWN_REMAINDER}: the tests written before this guard existed that still fake a composable of
 * ours. That list may only shrink. A file that no longer fakes one fails the guard until it is removed
 * from the list, and a new file that fakes one fails until it uses the real composable instead.
 * Stubbing a Nuxt or Vue framework global (`useRoute`, `useHead`, `useCookie`, `navigateTo`, `fetch` and so on)
 * is not replacing code of ours and is always allowed, and so is registering the real composable under its own
 * name (`vi.stubGlobal('useX', useX)`), which only makes the real one reachable as the global it is in the app.
 *
 * @module tests/config/noOwnMocks.test
 */

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The client root. */
const CLIENT_ROOT = resolve(import.meta.dirname, '..', '..')

/** The client's test folder. */
const TESTS_ROOT = join(CLIENT_ROOT, 'tests')

/** The folders whose exports Nuxt auto-imports, and so the names a test can fake as a global. */
const AUTO_IMPORT_FOLDERS: readonly string[] = ['composables', 'stores', 'utils'].map((name) => join(CLIENT_ROOT, name))

/** This file: it quotes the calls it hunts for, so it is the one file the scan skips. */
const THIS_FILE = import.meta.filename

/** A `vi.mock` or `vi.doMock` call with a literal module specifier, captured in group 2. */
const MOCK_CALL = /\bvi\.(?:mock|doMock)\(\s*(['"`])([^'"`]+)\1/g

/**
 * A `vi.stubGlobal` call with a literal name, captured in group 2. Group 3 holds the value when it is a bare
 * identifier (`vi.stubGlobal('useX', useX)`), which is how a test registers the real, imported composable under
 * its auto-import name; any other value (a function, a spy, an object) leaves group 3 undefined.
 */
const STUB_GLOBAL_CALL = /\bvi\.stubGlobal\(\s*(['"`])([^'"`]+)\1\s*(?:,\s*([A-Za-z0-9_$]+)\s*\))?/g

/** An exported function or constant of a client source file, captured in group 1. */
const EXPORTED_NAME = /export\s+(?:const|function|async function)\s+([A-Za-z0-9_$]+)/g

/** The start of a specifier that names a module of ours: an alias, a relative path or an import map entry. */
const OWN_MODULE = /^(?:~|@\/|\.|#)/

/**
 * The client tests that still replace a composable or store of ours with `vi.stubGlobal`, relative to
 * `client/tests`. Each one is a test to convert to the real composable; the list may only shrink.
 */
const KNOWN_REMAINDER: readonly string[] = [
  'helpers/authScreens.ts',
  'helpers/publicPage.ts',
  'helpers/templateGlobals.ts',
  'pages/about.test.ts',
  'pages/checkout/index.test.ts',
  'pages/contact.test.ts',
  'pages/cookie-policy.test.ts',
  'pages/domains/index.test.ts',
  'pages/hosting/index.test.ts',
  'pages/index.test.ts',
  'templates/aurora/auroraGlobals.ts',
]

/**
 * Lists every `.ts` file under a folder, skipping dependencies and build output.
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

/**
 * Finds the globals of ours that one file's text replaces with `vi.stubGlobal`. Registering the real composable
 * under its own name (`vi.stubGlobal('useX', useX)`) replaces nothing, so it is not counted.
 *
 * @param text - File contents.
 * @param ownNames - The auto-imported names the client defines itself.
 * @returns The names replaced, empty when the file replaces nothing of ours.
 */
const ownGlobalStubs = (text: string, ownNames: ReadonlySet<string>): string[] =>
  [...text.matchAll(STUB_GLOBAL_CALL)]
    .filter((match) => match[3] !== match[2])
    .map((match) => match[2] ?? '')
    .filter((name) => ownNames.has(name))

/** Every name the client's composables, stores and utils export, which Nuxt hands to every file as a global. */
const OWN_AUTO_IMPORTS: ReadonlySet<string> = new Set(
  AUTO_IMPORT_FOLDERS.flatMap(tsFiles).flatMap((path) =>
    [...readFileSync(path, 'utf8').matchAll(EXPORTED_NAME)].map((match) => match[1] ?? '')
  )
)

/**
 * The path of a test relative to the tests folder, with forward slashes.
 *
 * @param path - Absolute path of a test file.
 * @returns The path as the known list spells it.
 */
const testPath = (path: string): string => relative(TESTS_ROOT, path).replace(/\\/g, '/')

/** Every test file the guard reads. */
const SOURCES: readonly string[] = tsFiles(TESTS_ROOT).filter((path) => path !== THIS_FILE)

describe('no mocks of our own modules in the client tests', () => {
  it('reads the client tests and knows the composables Nuxt auto-imports', () => {
    expect(SOURCES.length).toBeGreaterThan(0)
    expect(OWN_AUTO_IMPORTS.has('useApi')).toBe(true)
  })

  it.each(SOURCES.map((path) => [testPath(path), path]))('%s mocks no module of ours', (_name, path) => {
    expect(ownMocks(readFileSync(path, 'utf8'))).toEqual([])
  })

  it.each(SOURCES.filter((path) => !KNOWN_REMAINDER.includes(testPath(path))).map((path) => [testPath(path), path]))(
    '%s fakes no composable or store of ours as a global',
    (_name, path) => {
      expect(ownGlobalStubs(readFileSync(path, 'utf8'), OWN_AUTO_IMPORTS)).toEqual([])
    }
  )

  it.each(KNOWN_REMAINDER.map((name) => [name]))('%s still fakes one, so it stays on the known list', (name) => {
    const path = join(TESTS_ROOT, name)
    expect(existsSync(path)).toBe(true)
    expect(ownGlobalStubs(readFileSync(path, 'utf8'), OWN_AUTO_IMPORTS)).not.toEqual([])
  })

  it('catches a store, a composable and a relative module being mocked', () => {
    expect(ownMocks(`vi.mock('~/stores/client', () => ({}))`)).toEqual(['~/stores/client'])
    expect(ownMocks(`vi.doMock('../composables/useApi', () => ({}))`)).toEqual(['../composables/useApi'])
    expect(ownMocks(`vi.mock(\n  '#imports',\n  () => ({}))`)).toEqual(['#imports'])
  })

  it('leaves the mocks of packages we do not own alone', () => {
    expect(ownMocks(`vi.mock('swiper/vue', () => ({}))`)).toEqual([])
    expect(ownMocks(`vi.mock('@vuepic/vue-datepicker', () => ({}))`)).toEqual([])
  })

  it('does not count the real composable registered under its own name as a fake', () => {
    const own = new Set(['useAuthMode', 'useClientStore'])

    expect(ownGlobalStubs(`vi.stubGlobal('useAuthMode', useAuthMode)`, own)).toEqual([])
    expect(ownGlobalStubs(`vi.stubGlobal( "useClientStore" , useClientStore )`, own)).toEqual([])
    expect(ownGlobalStubs(`vi.stubGlobal('useAuthMode', useOtherMode)`, own)).toEqual(['useAuthMode'])
    expect(ownGlobalStubs(`vi.stubGlobal('useAuthMode', useAuthMode.bind(null))`, own)).toEqual(['useAuthMode'])
  })

  it('catches a composable of ours being faked as a global and leaves framework globals alone', () => {
    const own = new Set(['usePortalSettings', 'useClientStore'])

    expect(ownGlobalStubs(`vi.stubGlobal('usePortalSettings', () => ({}))`, own)).toEqual(['usePortalSettings'])
    expect(ownGlobalStubs(`vi.stubGlobal("useClientStore", store)`, own)).toEqual(['useClientStore'])
    expect(ownGlobalStubs(`vi.stubGlobal('useRoute', () => ({}))`, own)).toEqual([])
    expect(ownGlobalStubs(`vi.stubGlobal('fetch', vi.fn())`, own)).toEqual([])
  })
})
