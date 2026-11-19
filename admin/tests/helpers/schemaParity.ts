/**
 * Parity helpers for the skeleton schemas: what a schema promises (its counts of sections, fields, columns, tiles and
 * tabs, its locale keys, its field keys), what `UiSkeletonOf` actually draws from it, and whether a schema is well formed.
 *
 * `tests/lint/skeletonSchemas.test.ts` runs them over every `views|layouts/*.schema.ts`, and the kit tests use them on
 * sample schemas, so one definition of "the skeleton is the screen" serves both.
 */
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import type { VueWrapper } from '@vue/test-utils'
import UiSkeletonOf from '@/components/ui/UiSkeletonOf.vue'
import { partsOf } from '@/utils/ui/skeletonParts'
import { SkeletonFieldKind, SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { SkeletonDetailCard, SkeletonField, SkeletonSchema, SkeletonSection, SkeletonStat, SkeletonTab, SkeletonTable } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from './i18n'

/** How many of each block a schema makes, or a skeleton draws. */
export interface SchemaCounts {
  /** Section cards of a form (a report's filter row counts as one). */
  sections: number
  /** Fields of a form or of a report's filters. */
  fields: number
  /** Table column headers, over every table of the screen. */
  columns: number
  /** Stat tiles. */
  tiles: number
  /** Tabs of a tab strip. */
  tabs: number
}

/** The largest number of side by side columns a form or a detail screen may list. */
export const MAX_SCHEMA_COLUMNS = 3

/**
 * The sections of every form block of a screen (a report's filter row is one section of its own).
 *
 * @param schema - A screen schema.
 * @returns The sections in display order.
 */
const sectionsOf = (schema: SkeletonSchema): SkeletonSection[] =>
  partsOf(schema).flatMap((part) => (part.kind === SkeletonPartKind.Form ? part.columns.flat() : []))

/**
 * The detail cards of every cards block of a screen.
 *
 * @param schema - A screen schema.
 * @returns The cards in display order.
 */
const cardsOf = (schema: SkeletonSchema): SkeletonDetailCard[] =>
  partsOf(schema).flatMap((part) => (part.kind === SkeletonPartKind.Cards ? part.columns.flat() : []))

/**
 * The fields of a schema's controls: a form's, or a report's filters.
 *
 * @param schema - A screen schema.
 * @returns The fields it lists, empty for a kind with no fields.
 */
export const fieldsOf = (schema: SkeletonSchema): SkeletonField[] => sectionsOf(schema).flatMap((section) => [...section.fields])

/**
 * The stat tiles of a screen: its blocks of tiles, or the key figures of a tab layout's header card.
 *
 * @param schema - A screen schema.
 * @returns The tiles in display order.
 */
const statsOf = (schema: SkeletonSchema): SkeletonStat[] => {
  const ofParts = partsOf(schema).flatMap((part) => (part.kind === SkeletonPartKind.Stats ? [...part.stats] : []))
  return schema.kind === SkeletonKind.Tabs ? [...(schema.stats ?? [])] : ofParts
}

/**
 * The tabs of a screen: a tab layout's, and every bare tab strip a block or a card holds.
 *
 * @param schema - A screen schema.
 * @returns The tabs in display order.
 */
export const tabsOf = (schema: SkeletonSchema): SkeletonTab[] => [
  ...(schema.kind === SkeletonKind.Tabs ? schema.tabs : []),
  ...partsOf(schema).flatMap((part) => (part.kind === SkeletonPartKind.Tabs ? [...part.tabs] : [])),
  ...cardsOf(schema).flatMap((card) => [...(card.tabs ?? [])]),
]

/**
 * Every table a schema holds.
 *
 * @param schema - A screen schema.
 * @returns Its tables, in display order.
 */
export const tablesOf = (schema: SkeletonSchema): SkeletonTable[] =>
  partsOf(schema).flatMap((part) => {
    if (part.kind === SkeletonPartKind.Table) {
      return [part.table]
    }
    return part.kind === SkeletonPartKind.Cards ? part.columns.flat().flatMap((card) => (card.table === undefined ? [] : [card.table])) : []
  })

/**
 * How many of each block a schema makes.
 *
 * @param schema - A screen schema.
 * @returns The counts a skeleton drawn from it must show.
 */
export const countsOf = (schema: SkeletonSchema): SchemaCounts => {
  return {
    sections: sectionsOf(schema).length,
    fields: fieldsOf(schema).length,
    columns: tablesOf(schema).reduce((total, table) => total + table.columns.length, 0),
    tiles: statsOf(schema).length,
    tabs: tabsOf(schema).length,
  }
}

/**
 * Mounts the skeleton of a schema with the real English catalogue.
 *
 * @param schema - The schema to draw.
 * @returns The mounted wrapper.
 */
export const mountSkeleton = (schema: SkeletonSchema): VueWrapper => mount(UiSkeletonOf, { props: { schema }, global: { plugins: [createTestI18n(), createPinia()] } })

/**
 * How many of each block a mounted skeleton draws.
 *
 * @param wrapper - A mounted `UiSkeletonOf`.
 * @returns The counts found in its markup.
 */
export const renderedCounts = (wrapper: VueWrapper): SchemaCounts => {
  // A rail layout that also draws a strip below md lists each tab twice; the rail's entries are the tabs then.
  const railEntries = wrapper.findAll('[data-testid="ui-sidebar-skeleton-entry"]').length
  return {
    sections: wrapper.findAll('[data-testid^="ui-form-section-"]').length,
    fields: wrapper.findAll('[data-testid^="ui-form-field-"]:not([data-testid="ui-form-field-bar"])').length,
    columns: wrapper.findAll('[role="columnheader"]').length,
    tiles: wrapper.findAll('[data-testid^="ui-stat-row-"]').length,
    tabs: railEntries > 0 ? railEntries : wrapper.findAll('[role="tab"]').length,
  }
}

/**
 * Every locale key a schema names: titles, captions, row captions, column labels, tile and tab labels, and the sentence of a report's empty state.
 *
 * @param schema - A screen schema.
 * @returns The keys, with repeats removed.
 */
export const localeKeysOf = (schema: SkeletonSchema): string[] => {
  const keys: (string | undefined)[] = [schema.header?.title, schema.header?.subtitle, schema.header?.backLabel]
  for (const section of sectionsOf(schema)) {
    keys.push(section.title)
  }
  for (const field of fieldsOf(schema)) {
    keys.push(field.label, ...(field.options ?? []))
  }
  for (const table of tablesOf(schema)) {
    keys.push(...table.columns.map((column) => column.label))
  }
  for (const card of cardsOf(schema)) {
    keys.push(card.title, ...(card.rows ?? []))
  }
  for (const part of partsOf(schema)) {
    if (part.kind === SkeletonPartKind.Toolbar) {
      keys.push(...(part.toolbar.filters ?? []))
    }
  }
  keys.push(...statsOf(schema).map((stat) => stat.label))
  if (schema.kind === SkeletonKind.Report) {
    keys.push(schema.emptyText)
  }
  keys.push(...tabsOf(schema).map((tab) => tab.label))
  return [...new Set(keys.filter((key): key is string => key !== undefined))]
}

/**
 * The keys of the fields of a form, which the screen binds in `#field-<key>` slots.
 *
 * @param schema - A screen schema.
 * @returns The field keys of a form or of the form blocks of a composite screen, empty for any other kind.
 */
export const formFieldKeysOf = (schema: SkeletonSchema): string[] =>
  schema.kind === SkeletonKind.Form || schema.kind === SkeletonKind.Composite ? fieldsOf(schema).map((field) => field.key) : []

/**
 * Finds the repeats in a list of keys.
 *
 * @param keys - Keys that must be unique.
 * @returns The keys that occur more than once.
 */
const repeated = (keys: readonly string[]): string[] => keys.filter((key, index) => keys.indexOf(key) !== index)

/**
 * Checks that a schema is well formed for its kind.
 *
 * @param schema - A default export of a schema module.
 * @returns What is wrong with it, one sentence each; empty when it is valid.
 */
export const problemsOf = (schema: SkeletonSchema): string[] => {
  const problems: string[] = []
  if (!Object.values(SkeletonKind).includes(schema.kind)) {
    return [`unknown kind ${String(schema.kind)}`]
  }
  for (const part of partsOf(schema)) {
    if ((part.kind === SkeletonPartKind.Form || part.kind === SkeletonPartKind.Cards) && (part.columns.length < 1 || part.columns.length > MAX_SCHEMA_COLUMNS)) {
      problems.push(`${part.columns.length} columns, expected 1 to ${MAX_SCHEMA_COLUMNS}`)
    }
  }
  if (schema.kind !== SkeletonKind.Report) {
    problems.push(...repeated(sectionsOf(schema).map((section) => section.key)).map((key) => `section key ${key} repeats`))
  }
  problems.push(...repeated(fieldsOf(schema).map((field) => field.key)).map((key) => `field key ${key} repeats`))
  problems.push(...sectionsOf(schema).filter((section) => section.fields.length === 0).map((section) => `section ${section.key} has no fields`))
  problems.push(...repeated(cardsOf(schema).map((card) => card.key)).map((key) => `card key ${key} repeats`))
  problems.push(...fieldsOf(schema).filter((field) => field.kind === SkeletonFieldKind.Radio && (field.options ?? []).length === 0).map((field) => `radio field ${field.key} has no options`))
  const tabs = tabsOf(schema)
  if (schema.kind === SkeletonKind.Tabs && schema.tabs.length === 0) {
    problems.push('a tab strip with no tabs')
  }
  problems.push(...repeated(tabs.map((tab) => tab.key)).map((key) => `tab key ${key} repeats`))
  for (const table of tablesOf(schema)) {
    if (table.columns.length === 0) {
      problems.push('a table with no columns')
    }
    problems.push(...repeated(table.columns.map((column) => column.key)).map((key) => `column key ${key} repeats`))
  }
  problems.push(...repeated(statsOf(schema).map((stat) => stat.key)).map((key) => `stat key ${key} repeats`))
  return problems
}
