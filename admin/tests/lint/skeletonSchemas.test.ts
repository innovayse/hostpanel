/**
 * Guard: every screen schema (`views|layouts/<Name>.schema.ts`) is valid and is really the screen it stands for.
 *
 * A schema drives two renders, the real screen and `UiSkeletonOf`, which is only a faithful skeleton while the
 * two stay one. For every schema file this suite checks that:
 * - the default export is a well formed schema of its kind;
 * - every locale key it names resolves in the English catalogue;
 * - the screen (`<Name>.vue` beside it) imports the schema;
 * - for a form, every field key has its `field-<key>` slot in the screen or in a component it imports;
 * - every table column label and tab label is drawn by the screen, either by its key or by iterating the schema's
 *   own `columns` or `tabs`;
 * - mounting `UiSkeletonOf` on it draws exactly the schema's sections, fields, columns, tiles and tabs.
 *
 * With no schema file yet the parametric part has nothing to run on, which is why the file-pairing check stands apart
 * and always runs.
 */
import { describe, expect, it } from 'vitest'
import type { SkeletonSchema } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from '../helpers/i18n'
import { countsOf, formFieldKeysOf, localeKeysOf, mountSkeleton, problemsOf, renderedCounts, tablesOf, tabsOf } from '../helpers/schemaParity'

/** A schema module: the schema is its default export. */
interface SchemaModule {
  /** The schema of the screen. */
  default: SkeletonSchema
}

/** The schema modules by path, such as `../../src/views/AddClientView.schema.ts`. */
const schemaModules = import.meta.glob<SchemaModule>('../../src/{views,layouts}/*.schema.ts', { eager: true })

/** The source text of every view and layout, by path. */
const screenSources = import.meta.glob<string>('../../src/{views,layouts}/*.vue', { eager: true, query: '?raw', import: 'default' })

/** The source text of every component, by path, for the components a screen imports. */
const componentSources = import.meta.glob<string>('../../src/components/**/*.vue', { eager: true, query: '?raw', import: 'default' })

/** The source text of every schema module, by path, to read the locale keys it writes itself. */
const schemaSources = import.meta.glob<string>('../../src/{views,layouts}/*.schema.ts', { eager: true, query: '?raw', import: 'default' })

/** The source text of the tables a layout schema builds its tabs from (the client sections, the report registry, the integration categories). */
const tabTableSources = Object.values({
  ...import.meta.glob<string>('../../src/lib/{clientSections,reports}.ts', { eager: true, query: '?raw', import: 'default' }),
  ...import.meta.glob<string>('../../src/types/integrationmeta.ts', { eager: true, query: '?raw', import: 'default' }),
})

/** One schema file with its screen. */
interface SchemaCase {
  /** The screen's name with its folder, such as `views/AddClientView`. */
  name: string
  /** The schema. */
  schema: SkeletonSchema
  /** The path of the screen's `.vue` file, as the glob keys it. */
  screenPath: string
  /** The source text of the schema module. */
  schemaSource: string
}

/**
 * Names the screen a schema file belongs to.
 *
 * @param schemaPath - The path of the schema module as the glob keys it.
 * @returns The `.vue` path beside it.
 */
const screenPathOf = (schemaPath: string): string => schemaPath.replace(/\.schema\.ts$/, '.vue')

/** The schema files found, as cases. */
const cases: SchemaCase[] = Object.entries(schemaModules).map(([path, module]) => ({
  name: path.replace('../../src/', '').replace(/\.schema\.ts$/, ''),
  schema: module.default,
  screenPath: screenPathOf(path),
  schemaSource: schemaSources[path] ?? '',
}))

/**
 * Lists the components a source imports from `@/components`, and those they import in turn.
 *
 * @param source - The source text of a screen or a component.
 * @param seen - The glob keys already collected, so a component imported twice is read once.
 * @returns The source text of each component found.
 */
const importedComponents = (source: string, seen: Set<string> = new Set()): string[] =>
  [...source.matchAll(/from '@\/components\/([^']+\.vue)'/g)].flatMap((match) => {
    const key = `../../src/components/${match[1]}`
    if (seen.has(key)) {
      return []
    }
    seen.add(key)
    const component = componentSources[key] ?? ''
    return [component, ...importedComponents(component, seen)]
  })

/**
 * Reads a screen together with the components it imports, however deep: the captions a screen names may be drawn by a
 * tab, and the tab's table by a section of it.
 *
 * @param screenPath - The glob key of the screen.
 * @returns The source of the screen followed by the source of each component it reaches through `@/components` imports.
 */
const screenWithComponents = (screenPath: string): string => {
  const source = screenSources[screenPath] ?? ''
  return [source, ...importedComponents(source)].join('\n')
}

/**
 * The screens that are only a shell around one shared component, which owns the schema's cards because another screen
 * draws the same component (the plugin settings tab): the screen's name with its folder, and the component it hands over to.
 */
const DELEGATING_SCREENS: Readonly<Record<string, string>> = {
  'views/IntegrationDetailView': '../../src/components/integrations/IntegrationConfigPanel.vue',
}

/** The English catalogue, asked whether a key resolves. */
const i18n = createTestI18n()

describe('skeleton schema files', () => {
  it('sit beside a view or layout of the same name', () => {
    for (const path of Object.keys(schemaModules)) {
      expect(screenSources, `${path} has no screen beside it`).toHaveProperty([screenPathOf(path)])
    }
  })
})

describe.skipIf(cases.length === 0)('every skeleton schema', () => {
  describe.each(cases)('$name', ({ name: caseName, schema, screenPath, schemaSource }) => {
    const source = screenWithComponents(screenPath)

    it('is well formed for its kind', () => {
      expect(problemsOf(schema)).toEqual([])
    })

    it('names only locale keys that resolve in English', () => {
      expect(localeKeysOf(schema).filter((key) => !i18n.global.te(key))).toEqual([])
    })

    it('is imported by its own screen, not only by a component the screen draws', () => {
      const name = screenPath.split('/').pop()?.replace(/\.vue$/, '') ?? ''

      const delegate = DELEGATING_SCREENS[caseName]
      const reader = delegate === undefined ? (screenSources[screenPath] ?? '') : (componentSources[delegate] ?? '')

      expect(reader).toMatch(new RegExp(`from '(\\./|@/(views|layouts)/)${name}\\.schema'`))
    })

    it('has a field slot in the screen for every field of a form', () => {
      const missing = formFieldKeysOf(schema).filter((key) => !source.includes(`field-${key}`))

      expect(missing).toEqual([])
    })

    it('draws every column caption, by its key or by iterating the schema columns', () => {
      const iterates = /\bschema\.table\.columns\b|\breportColumns\((props\.)?schema\.table|<UiSchemaTableHead\b/.test(source)
      const missing = tablesOf(schema).flatMap((table) => table.columns.flatMap((column) => (column.label === undefined || iterates || source.includes(column.label) ? [] : [column.label])))

      expect(missing).toEqual([])
    })

    it('draws every tab caption, by its key or by iterating the schema tabs', () => {
      const iterates = /\bschema\.tabs\b|\btabItemsOfSchema\(|\bCLIENT_NAV\b/.test(source)
      const missing = tabsOf(schema).filter((tab) => !iterates && !source.includes(tab.label)).map((tab) => tab.label)

      expect(missing).toEqual([])
    })

    it('has every tab caption written as a locale key in the schema, the screen or the table its tabs are built from', () => {
      const written = [schemaSource, source, ...tabTableSources].join('\n')
      const missing = tabsOf(schema).filter((tab) => !written.includes(`'${tab.label}'`)).map((tab) => tab.label)

      expect(missing).toEqual([])
    })

    it('draws a skeleton with exactly its sections, fields, columns, tiles and tabs', () => {
      expect(renderedCounts(mountSkeleton(schema))).toEqual(countsOf(schema))
    })
  })
})
