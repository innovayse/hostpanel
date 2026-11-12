/**
 * Guard: no test replaces one of our own modules. A store, an api composable, a composable or a helper
 * of ours runs for real in every test; only what we do not own is substituted, and the network is
 * stubbed below `useApi` (the admin) or `$fetch` (the client) through the fetch and stub-server helpers.
 *
 * Two forms of replacing are caught. `vi.mock` and `vi.doMock` of a module of ours are refused outright
 * in both apps. In the client, `vi.stubGlobal` of one of our own auto-imported composables or stores is
 * the same replacement by another API (a Nuxt auto-import is a global under test), and it is refused too,
 * except in the files of {@link KNOWN_REMAINDER}: the tests written before this guard existed that still
 * fake a composable of ours. That list may only shrink. A file that no longer fakes one fails the guard
 * until it is removed from the list, and a new file that fakes one fails until it uses the real
 * composable instead. Stubbing a Nuxt or Vue framework global (`useRoute`, `useHead`, `useCookie`,
 * `navigateTo`, `fetch`, ...) is not replacing code of ours and is always allowed.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

/** Repository root: the admin and client folders sit side by side beneath it. */
const REPO = join(__dirname, '..', '..', '..')

/** The test folders of both apps. */
const TEST_FOLDERS: readonly string[] = [join(REPO, 'admin', 'tests'), join(REPO, 'client', 'tests')]

/** The client folders whose exports Nuxt auto-imports, and so the names a test can fake as a global. */
const CLIENT_AUTO_IMPORT_FOLDERS: readonly string[] = ['composables', 'stores', 'utils'].map((name) => join(REPO, 'client', name))

/** This file: it quotes the calls it hunts for, so it is the one file the scan skips. */
const THIS_FILE = __filename

/** A `vi.mock` or `vi.doMock` call with a literal module specifier, captured in group 2. */
const MOCK_CALL = /\bvi\.(?:mock|doMock)\(\s*(['"`])([^'"`]+)\1/g

/** A `vi.stubGlobal` call with a literal name, captured in group 2. */
const STUB_GLOBAL_CALL = /\bvi\.stubGlobal\(\s*(['"`])([^'"`]+)\1/g

/** An exported function or constant of a client source file, captured in group 1. */
const EXPORTED_NAME = /export\s+(?:const|function|async function)\s+([A-Za-z0-9_$]+)/g

/** The start of a specifier that names a module of ours: an alias, a relative path or an import map entry. */
const OWN_MODULE = /^(?:~|@\/|\.|#)/

/**
 * The client tests that still replace a composable or store of ours with `vi.stubGlobal`, relative to
 * `client/tests`. Each one is a test to convert to the real composable; the list may only shrink.
 */
const KNOWN_REMAINDER: readonly string[] = [
  'app.test.ts',
  'components/account/ProfileTab.test.ts',
  'components/layout/Footer.test.ts',
  'components/layout/Header.test.ts',
  'components/sections/SectionsApps.test.ts',
  'components/sections/SectionsFAQ.test.ts',
  'components/sections/SectionsGuarantees.test.ts',
  'components/sections/SectionsHero.test.ts',
  'components/sections/SectionsPerformance.test.ts',
  'components/sections/SectionsSecurity.test.ts',
  'components/sections/SectionsSupport.test.ts',
  'components/ui/UiCookieBanner.test.ts',
  'components/ui/UiFloatingActions.test.ts',
  'components/ui/UiThemeToggle.test.ts',
  'components/ui/UiWhatsAppButton.test.ts',
  'composables/useAboutText.test.ts',
  'composables/useAppColorMode.test.ts',
  'composables/useBrandLogo.test.ts',
  'composables/useContactDetails.test.ts',
  'composables/useContactHours.test.ts',
  'composables/useGuarantees.test.ts',
  'composables/useHomeSections.test.ts',
  'composables/useInstallerApps.test.ts',
  'composables/useLocalizedSetting.test.ts',
  'composables/usePerformanceStats.test.ts',
  'composables/useSchemaOrg.test.ts',
  'composables/useSecurityFeatures.test.ts',
  'composables/useSeo.test.ts',
  'composables/useSiteIdentity.test.ts',
  'composables/useSupportChannels.test.ts',
  'composables/useTemplate.test.ts',
  'helpers/authScreens.ts',
  'helpers/publicPage.ts',
  'helpers/templateGlobals.ts',
  'layouts/client.test.ts',
  'layouts/default.test.ts',
  'pages/about.test.ts',
  'pages/checkout/index.test.ts',
  'pages/client/dashboard.test.ts',
  'pages/client/tickets/[id].test.ts',
  'pages/contact.test.ts',
  'pages/cookie-policy.test.ts',
  'pages/domains/index.test.ts',
  'pages/hosting/index.test.ts',
  'pages/index.test.ts',
  'pages/payment/result.test.ts',
  'plugins/brand-tokens.test.ts',
  'plugins/client-auth.test.ts',
  'plugins/color-mode.test.ts',
  'plugins/live-chat.test.ts',
  'plugins/tracking.client.test.ts',
  'templates/aurora/auroraGlobals.ts',
  'templates/aurora/sections/AuroraFaq.test.ts',
  'templates/aurora/sections/AuroraHero.test.ts',
  'templates/classic/pages/ClassicHome.test.ts',
  'templates/nova/features.test.ts',
  'templates/nova/pages/NovaHome.test.ts',
  'templates/nova/sections/NovaApps.test.ts',
  'templates/nova/sections/NovaGuarantees.test.ts',
  'templates/nova/sections/NovaPerformance.test.ts',
  'templates/nova/sections/NovaSecurity.test.ts',
  'templates/nova/sections/NovaSupport.test.ts',
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
    if (name === 'node_modules' || name === '.nuxt' || name === '.output' || name.startsWith('.')) {
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
 * Finds the globals of ours that one file's text replaces with `vi.stubGlobal`.
 *
 * @param text - File contents.
 * @param ownNames - The auto-imported names the client defines itself.
 * @returns The names replaced, empty when the file replaces nothing of ours.
 */
const ownGlobalStubs = (text: string, ownNames: ReadonlySet<string>): string[] =>
  [...text.matchAll(STUB_GLOBAL_CALL)].map((match) => match[2] ?? '').filter((name) => ownNames.has(name))

/** Every name the client's composables, stores and utils export, which Nuxt hands to every file as a global. */
const OWN_AUTO_IMPORTS: ReadonlySet<string> = new Set(
  CLIENT_AUTO_IMPORT_FOLDERS.flatMap(tsFiles).flatMap((path) =>
    [...readFileSync(path, 'utf8').matchAll(EXPORTED_NAME)].map((match) => match[1] ?? '')
  )
)

/** The path of a test relative to its app's `tests` folder, with forward slashes. */
const testPath = (path: string): string => relative(join(REPO, 'client', 'tests'), path).replace(/\\/g, '/')

/** Every test file the guard reads. */
const SOURCES: readonly string[] = TEST_FOLDERS.flatMap(tsFiles).filter((path) => path !== THIS_FILE)

/** The client's test files, the only ones `vi.stubGlobal` of an auto-import can affect. */
const CLIENT_SOURCES: readonly string[] = SOURCES.filter((path) => path.startsWith(join(REPO, 'client', 'tests')))

describe('no mocks of our own modules', () => {
  it('reads the tests of both apps', () => {
    expect(SOURCES.some((path) => path.includes(join('admin', 'tests')))).toBe(true)
    expect(SOURCES.some((path) => path.includes(join('client', 'tests')))).toBe(true)
    expect(OWN_AUTO_IMPORTS.has('useApi')).toBe(true)
  })

  it.each(SOURCES.map((path) => [relative(REPO, path), path]))('%s mocks no module of ours', (_name, path) => {
    expect(ownMocks(readFileSync(path, 'utf8'))).toEqual([])
  })

  it.each(CLIENT_SOURCES.filter((path) => !KNOWN_REMAINDER.includes(testPath(path))).map((path) => [testPath(path), path]))(
    '%s fakes no composable or store of ours as a global',
    (_name, path) => {
      expect(ownGlobalStubs(readFileSync(path, 'utf8'), OWN_AUTO_IMPORTS)).toEqual([])
    }
  )

  it.each(KNOWN_REMAINDER.map((name) => [name]))('%s still fakes one, so it stays on the known list', (name) => {
    const path = join(REPO, 'client', 'tests', name)
    expect(existsSync(path)).toBe(true)
    expect(ownGlobalStubs(readFileSync(path, 'utf8'), OWN_AUTO_IMPORTS)).not.toEqual([])
  })

  it('catches a store, a composable and a relative module being mocked', () => {
    expect(ownMocks(`vi.mock('~/stores/client', () => ({}))`)).toEqual(['~/stores/client'])
    expect(ownMocks(`vi.mock("@/composables/useApi", () => ({}))`)).toEqual(['@/composables/useApi'])
    expect(ownMocks(`vi.doMock('../src/lib/money', () => ({}))`)).toEqual(['../src/lib/money'])
    expect(ownMocks(`vi.mock(\n  '#imports',\n  () => ({}))`)).toEqual(['#imports'])
  })

  it('leaves the mocks of packages we do not own alone', () => {
    expect(ownMocks(`vi.mock('chart.js', () => ({}))`)).toEqual([])
    expect(ownMocks(`vi.mock('@vuepic/vue-datepicker', () => ({}))`)).toEqual([])
    expect(ownMocks(`vi.mock('swiper/vue', () => ({}))`)).toEqual([])
  })

  it('catches a composable of ours being faked as a global and leaves framework globals alone', () => {
    const own = new Set(['usePortalSettings', 'useClientStore'])

    expect(ownGlobalStubs(`vi.stubGlobal('usePortalSettings', () => ({}))`, own)).toEqual(['usePortalSettings'])
    expect(ownGlobalStubs(`vi.stubGlobal("useClientStore", store)`, own)).toEqual(['useClientStore'])
    expect(ownGlobalStubs(`vi.stubGlobal('useRoute', () => ({}))`, own)).toEqual([])
    expect(ownGlobalStubs(`vi.stubGlobal('fetch', vi.fn())`, own)).toEqual([])
  })
})
