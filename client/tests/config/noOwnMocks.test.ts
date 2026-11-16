/**
 * Guard: no client test replaces one of our own modules. A store, an api composable, a composable or a
 * helper of ours runs for real in every test; only what we do not own is substituted, and the network is
 * stubbed below `$fetch` through the stub-server helpers. The admin has the same guard for its tests in
 * `admin/tests/lint/noOwnMocks.test.ts`: the two apps cannot read each other's files, so each reads its own.
 *
 * Two forms of replacing are caught. `vi.mock` and `vi.doMock` of a module of ours are refused outright.
 * `vi.stubGlobal` of one of our own auto-imported composables or stores is the same replacement by
 * another API (a Nuxt auto-import is a global under test), and it is refused too, in every file: a test that
 * needs one uses the real composable. The one global stand-in file, `vitest.setup.ts`, is read as well and may
 * define no composable or store of ours, by `define`, by `vi.stubGlobal` or by assigning to `globalThis`.
 * Stubbing a Nuxt or Vue framework global (`useRoute`, `useHead`, `useCookie`, `navigateTo`, `fetch` and so on)
 * is not replacing code of ours and is always allowed. So is registering the real composable under its own
 * name (`vi.stubGlobal('useX', useX)`), which only makes the real one reachable as the global it is in the app:
 * the bare identifier must be bound in the file by an import of that name from a composable, store or util of the
 * client (a static import or a destructured `await import(...)`), never a local value or a test helper's export.
 *
 * `usePortalSettingsServer` registers its argument by a dynamic name, which the `stubGlobal` scan cannot see, so
 * the guard reads the argument itself: every entry must be a bare identifier bound the same way, the real one.
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

/** A call of the portal settings helper, up to the first character of its argument (an object, a name or `)`). */
const SERVER_SETUP_CALL = /\busePortalSettingsServer\(\s*/g

/** One entry of the helper's argument that is the real thing: a bare identifier, or `name: name`, captured in group 1. */
const REAL_ENTRY = /^([A-Za-z0-9_$]+)(?:\s*:\s*\1)?$/

/**
 * A named import statement or a destructured dynamic import, which may span several lines: its specifier list
 * is group 1 or group 2, its source group 4.
 */
const NAMED_IMPORT = /(?:\bimport\s+(?:type\s+)?\{([^}]*)\}\s*from\s*|\{([^}]*)\}\s*=\s*await\s+import\(\s*)(['"])([^'"]+)\3/g

/** The source of a real client module the helper may register: a composable, store or util under the `~` or `@` alias. */
const REAL_SOURCE = /^[~@]\/(?:composables|stores|utils)\//

/** An exported function or constant of a client source file, captured in group 1. */
const EXPORTED_NAME = /export\s+(?:const|function|async function)\s+([A-Za-z0-9_$]+)/g

/** The start of a specifier that names a module of ours: an alias, a relative path or an import map entry. */
const OWN_MODULE = /^(?:~|@\/|\.|#)/

/** The global test setup, which defines the Nuxt auto-imports a unit test lacks. */
const SETUP_FILE = join(CLIENT_ROOT, 'vitest.setup.ts')

/** A stand-in the setup defines as a global by name (`define('useX', ...)`), the name captured in group 2. */
const SETUP_DEFINITION = /\bdefine\(\s*(['"`])([^'"`]+)\1/g

/** An assignment to a property of `globalThis` (`globalThis.useX = ...`, `globalThis['useX'] = ...`), the name in group 1 or group 3. */
const GLOBAL_ASSIGNMENT = /\bglobalThis(?:\.([A-Za-z0-9_$]+)|\[\s*(['"`])([^'"`]+)\2\s*\])\s*=(?!=)/g

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
 * Tells whether a file binds a name, under that same name, from one of our real modules, by a named import or a
 * destructured `await import(...)`. This is the one check of what counts as the real thing, shared by the
 * `stubGlobal` scan and the portal settings helper scan.
 *
 * A name imported from a test helper, or renamed on import (`import { fake as useX }`), is not the real thing.
 *
 * @param text - File contents.
 * @param name - The identifier being registered.
 * @returns True when an import binds `name` to an export of the same name in a composable, store or util.
 */
const importsRealModule = (text: string, name: string): boolean =>
  [...text.matchAll(NAMED_IMPORT)].some((statement) =>
    REAL_SOURCE.test(statement[4] ?? '') &&
    (statement[1] ?? statement[2] ?? '').split(',').some((specifier) => specifier.trim().replace(/^type\s+/, '') === name)
  )

/**
 * Finds the globals of ours that one file's text replaces with `vi.stubGlobal`. Registering the real composable
 * under its own name (`vi.stubGlobal('useX', useX)`) replaces nothing, so it is not counted, but only when `useX`
 * is bound in the file by an import of the real module: a local value of that name is a fake.
 *
 * @param text - File contents.
 * @param ownNames - The auto-imported names the client defines itself.
 * @returns The names replaced, empty when the file replaces nothing of ours.
 */
const ownGlobalStubs = (text: string, ownNames: ReadonlySet<string>): string[] =>
  [...text.matchAll(STUB_GLOBAL_CALL)]
    .filter((match) => !(match[3] === match[2] && importsRealModule(text, match[2] ?? '')))
    .map((match) => match[2] ?? '')
    .filter((name) => ownNames.has(name))

/**
 * Finds the globals of ours that the setup file stands in for, by `define`, by `vi.stubGlobal` or by assigning to
 * `globalThis`.
 *
 * @param text - Contents of the setup file.
 * @param ownNames - The auto-imported names the client defines itself.
 * @returns The names defined, empty when the setup stands in for nothing of ours.
 */
const ownSetupDefinitions = (text: string, ownNames: ReadonlySet<string>): string[] =>
  [
    ...[...text.matchAll(SETUP_DEFINITION)].map((match) => match[2] ?? ''),
    ...[...text.matchAll(GLOBAL_ASSIGNMENT)].map((match) => match[1] ?? match[3] ?? ''),
    ...ownGlobalStubs(text, ownNames),
  ].filter((name) => ownNames.has(name))

/**
 * Reads the body of the object literal that opens at an index.
 *
 * @param text - File contents.
 * @param start - Index of the opening brace.
 * @returns What lies between the braces, or null when the object never closes.
 */
const objectBody = (text: string, start: number): string | null => {
  let depth = 0
  for (let index = start; index < text.length; index++) {
    if (text[index] === '{') {
      depth++
    } else if (text[index] === '}') {
      depth--
      if (depth === 0) {
        return text.slice(start + 1, index)
      }
    }
  }
  return null
}

/**
 * Finds what `usePortalSettingsServer(...)` registers that is not a real, imported composable or helper. The helper
 * stubs each entry as a global by a dynamic name, so a fake would pass the `vi.stubGlobal` scan unseen; here every
 * entry of the argument must be a bare identifier that the file imports under its own name from a composable,
 * store or util of the client, not from a test helper. The argument may be an object literal or
 * the name of a `const` object literal in the same file.
 *
 * @param text - File contents.
 * @returns The entries (or the whole argument) that are not a bare imported identifier, empty when all are real.
 */
const fakedRegistrations = (text: string): string[] =>
  [...text.matchAll(SERVER_SETUP_CALL)].flatMap((call) => {
    const start = (call.index ?? 0) + call[0].length
    if (text[start] === ')') {
      return []
    }
    let body: string | null
    if (text[start] === '{') {
      body = objectBody(text, start)
    } else {
      const name = /^([A-Za-z0-9_$]+)\s*\)/.exec(text.slice(start))?.[1]
      const declaration = name ? new RegExp(String.raw`\bconst\s+${name}\s*=\s*\{`).exec(text) : null
      body = declaration ? objectBody(text, declaration.index + declaration[0].length - 1) : null
    }
    if (body === null) {
      return [text.slice(start, text.indexOf(')', start))]
    }
    return body.split(',').map((entry) => entry.trim()).filter((entry) => entry !== '').filter((entry) => {
      const name = REAL_ENTRY.exec(entry)?.[1]
      return !name || !importsRealModule(text, name)
    })
  })

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
 * @returns The path as the test names spell it.
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

  it.each(SOURCES.map((path) => [testPath(path), path]))(
    '%s fakes no composable or store of ours as a global',
    (_name, path) => {
      expect(ownGlobalStubs(readFileSync(path, 'utf8'), OWN_AUTO_IMPORTS)).toEqual([])
    }
  )

  it.each(SOURCES.map((path) => [testPath(path), path]))('%s registers only real composables through the portal settings helper', (_name, path) => {
    expect(fakedRegistrations(readFileSync(path, 'utf8'))).toEqual([])
  })

  it('stands in for no composable or store of ours in the global test setup', () => {
    expect(ownSetupDefinitions(readFileSync(SETUP_FILE, 'utf8'), OWN_AUTO_IMPORTS)).toEqual([])
  })

  it('catches a composable of ours defined as a setup stand-in and leaves the Nuxt ones alone', () => {
    const own = new Set(['useSiteIdentity'])

    expect(ownSetupDefinitions(`define('useSiteIdentity', () => ({}))`, own)).toEqual(['useSiteIdentity'])
    expect(ownSetupDefinitions(`define("useSiteIdentity", () => ({}))`, own)).toEqual(['useSiteIdentity'])
    expect(ownSetupDefinitions(`define('useRoute', () => ({}))`, own)).toEqual([])
  })

  it('catches a composable of ours stubbed or assigned as a global in the setup', () => {
    const own = new Set(['useSiteIdentity'])
    const real = "import { useSiteIdentity } from '~/composables/useSiteIdentity'\n"

    expect(ownSetupDefinitions(`vi.stubGlobal('useSiteIdentity', () => ({}))`, own)).toEqual(['useSiteIdentity'])
    expect(ownSetupDefinitions(`${real}vi.stubGlobal('useSiteIdentity', useSiteIdentity)`, own)).toEqual([])
    expect(ownSetupDefinitions('globalThis.useSiteIdentity = () => ({})', own)).toEqual(['useSiteIdentity'])
    expect(ownSetupDefinitions(`globalThis['useSiteIdentity'] = () => ({})`, own)).toEqual(['useSiteIdentity'])
    expect(ownSetupDefinitions('globalThis.ResizeObserver = Observer', own)).toEqual([])
    expect(ownSetupDefinitions('globalThis.useSiteIdentity === undefined', own)).toEqual([])
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

    const imports = "import { useAuthMode } from '~/composables/useAuthMode'\nimport { useClientStore } from '~/stores/client'\n"

    expect(ownGlobalStubs(`${imports}vi.stubGlobal('useAuthMode', useAuthMode)`, own)).toEqual([])
    expect(ownGlobalStubs(`${imports}vi.stubGlobal( "useClientStore" , useClientStore )`, own)).toEqual([])
    expect(ownGlobalStubs(`${imports}vi.stubGlobal('useAuthMode', useOtherMode)`, own)).toEqual(['useAuthMode'])
    expect(ownGlobalStubs(`${imports}vi.stubGlobal('useAuthMode', useAuthMode.bind(null))`, own)).toEqual(['useAuthMode'])
  })

  it('counts a same-named value that is not the real import as a fake global', () => {
    const own = new Set(['useAuthMode'])
    const stub = "\nvi.stubGlobal('useAuthMode', useAuthMode)"
    const local = `const useAuthMode = () => ({})${stub}`
    const helper = `import { useAuthMode } from '~/tests/helpers/fakes'${stub}`
    const renamed = `import { fake as useAuthMode } from '~/composables/useAuthMode'${stub}`
    const real = `import { useAuthMode } from '~/composables/useAuthMode'${stub}`
    const dynamic = `const { useAuthMode } = await import('~/composables/useAuthMode')${stub}`
    const dynamicHelper = `const { useAuthMode } = await import('~/tests/helpers/fakes')${stub}`
    const dynamicRenamed = `const { fake: useAuthMode } = await import('~/composables/useAuthMode')${stub}`

    expect(ownGlobalStubs(local, own)).toEqual(['useAuthMode'])
    expect(ownGlobalStubs(helper, own)).toEqual(['useAuthMode'])
    expect(ownGlobalStubs(renamed, own)).toEqual(['useAuthMode'])
    expect(ownGlobalStubs(real, own)).toEqual([])
    expect(ownGlobalStubs(dynamic, own)).toEqual([])
    expect(ownGlobalStubs(dynamicHelper, own)).toEqual(['useAuthMode'])
    expect(ownGlobalStubs(dynamicRenamed, own)).toEqual(['useAuthMode'])
  })

  it('catches a composable of ours being faked as a global and leaves framework globals alone', () => {
    const own = new Set(['usePortalSettings', 'useClientStore'])

    expect(ownGlobalStubs(`vi.stubGlobal('usePortalSettings', () => ({}))`, own)).toEqual(['usePortalSettings'])
    expect(ownGlobalStubs(`vi.stubGlobal("useClientStore", store)`, own)).toEqual(['useClientStore'])
    expect(ownGlobalStubs(`vi.stubGlobal('useRoute', () => ({}))`, own)).toEqual([])
    expect(ownGlobalStubs(`vi.stubGlobal('fetch', vi.fn())`, own)).toEqual([])
  })

  it('accepts the helper called with no argument or with imported identifiers only', () => {
    const imports = "import { useX } from '~/composables/useX'\nimport { useY } from '~/composables/useY'\n"

    expect(fakedRegistrations('usePortalSettingsServer()')).toEqual([])
    expect(fakedRegistrations(`${imports}usePortalSettingsServer({ useX, useY: useY })`)).toEqual([])
    expect(fakedRegistrations(`${imports}const REAL = { useX, useY }\nusePortalSettingsServer(REAL)`)).toEqual([])
  })

  it('catches a name imported under a rename or from a test helper instead of the real module', () => {
    const renamed = "import { fake as useX } from '~/composables/useX'\n"
    const helper = "import { useX } from '~/tests/helpers/fakes'\n"

    expect(fakedRegistrations(`${renamed}usePortalSettingsServer({ useX })`)).toEqual(['useX'])
    expect(fakedRegistrations(`${helper}usePortalSettingsServer({ useX })`)).toEqual(['useX'])
  })

  it('reads an import block that spans several lines', () => {
    const real = "import {\n  useX,\n  useY,\n} from '~/composables/useX'\n"
    const renamed = "import {\n  useX,\n  fake as useY,\n} from '~/composables/useX'\n"

    expect(fakedRegistrations(`${real}usePortalSettingsServer({ useX, useY })`)).toEqual([])
    expect(fakedRegistrations(`${renamed}usePortalSettingsServer({ useX, useY })`)).toEqual(['useY'])
  })

  it('reads a destructured dynamic import as a real binding for the helper too', () => {
    const real = "const { useX } = await import('~/composables/useX')\n"
    const helper = "const { useX } = await import('~/tests/helpers/fakes')\n"

    expect(fakedRegistrations(`${real}usePortalSettingsServer({ useX })`)).toEqual([])
    expect(fakedRegistrations(`${helper}usePortalSettingsServer({ useX })`)).toEqual(['useX'])
  })

  it('catches a fake, a local stand-in or an unreadable argument passed to the helper', () => {
    const imports = "import { useX } from '~/composables/useX'\n"

    expect(fakedRegistrations(`${imports}usePortalSettingsServer({ useX: () => ({ a: 1, b: 2 }) })`)).not.toEqual([])
    expect(fakedRegistrations(`${imports}usePortalSettingsServer({ useX: useOther })`)).toEqual(['useX: useOther'])
    expect(fakedRegistrations('usePortalSettingsServer({ useLocal })')).toEqual(['useLocal'])
    expect(fakedRegistrations(`${imports}const FAKES = { useX: vi.fn() }\nusePortalSettingsServer(FAKES)`)).toEqual(['useX: vi.fn()'])
    expect(fakedRegistrations('usePortalSettingsServer(fakes.all)')).toEqual(['fakes.all'])
  })
})
