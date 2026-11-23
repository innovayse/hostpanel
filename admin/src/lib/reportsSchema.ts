/**
 * Report screens read from their skeleton schema.
 *
 * A report's `views/<Name>.schema.ts` names its title, its filter captions, its stat tiles and its table
 * columns as locale keys. The real screen resolves them here, so the captions it draws and the ones the
 * skeleton draws while the chunk downloads are the same list; the screen adds only what a schema cannot
 * hold (a cell format, a currency key, the values).
 */
import type { FieldOption } from '@/components/ui/UiAdvancedFilters.vue'
import type { ReportColumn, ReportColumnExtra } from '@/types/reportcolumn'
import type { ReportQueryRange } from '@/types/reportquery'
import type { ReportStat, ReportStatValue } from '@/types/reportstat'
import type { ReportSkeletonSchema, SkeletonColumn, SkeletonField, SkeletonStat, SkeletonTable } from '@/utils/ui/skeletonSchema'

/** Resolves a locale key to its text; the `t` of `useI18n`. */
export type ReportTranslate = (key: string) => string

/** The title and the line under it of a report, translated. */
export interface ReportHeadline {
  /** Title; empty when the schema names none. */
  title: string
  /** Line under the title; absent when the schema has none. */
  description?: string
}

/**
 * Translates the header of a report schema.
 *
 * @param schema - The report schema.
 * @param t - Resolves a locale key.
 * @returns The title and the optional description.
 */
export const reportHeadline = (schema: ReportSkeletonSchema, t: ReportTranslate): ReportHeadline => ({
  title: schema.header?.title === undefined ? '' : t(schema.header.title),
  description: schema.header?.subtitle === undefined ? undefined : t(schema.header.subtitle),
})

/**
 * Turns one schema column into a report table column.
 *
 * @param column - The schema column.
 * @param t - Resolves a locale key.
 * @param extra - The cell format the screen adds.
 * @returns The column, with the caption translated and the alignment, kind and width of the schema.
 */
export const reportColumn = (column: SkeletonColumn, t: ReportTranslate, extra: ReportColumnExtra = {}): ReportColumn => ({
  key: column.key,
  label: column.label === undefined ? '' : t(column.label),
  align: column.align,
  size: column.size,
  kind: column.kind,
  ...extra,
})

/**
 * Turns the columns of a schema table into report table columns.
 *
 * A column whose key is in `expansions` and whose list is not empty is replaced by that list: the columns a
 * report knows only from its data (a day of the month, a currency). While the data is missing the schema's
 * own placeholder column stays, so the table keeps the shape the skeleton drew.
 *
 * @param table - The schema table; none gives no column.
 * @param t - Resolves a locale key.
 * @param extras - Cell formats by column key.
 * @param expansions - Columns that replace a placeholder column, by its key.
 * @returns The columns in display order.
 */
export const reportColumns = (
  table: SkeletonTable | undefined,
  t: ReportTranslate,
  extras: Readonly<Record<string, ReportColumnExtra>> = {},
  expansions: Readonly<Record<string, readonly ReportColumn[]>> = {},
): ReportColumn[] =>
  (table?.columns ?? []).flatMap((column) => {
    const expansion = expansions[column.key]
    return expansion !== undefined && expansion.length > 0 ? [...expansion] : [reportColumn(column, t, extras[column.key])]
  })

/**
 * Turns the stat tiles of a schema into the tiles of `ReportStatRow`.
 *
 * @param stats - The schema tiles; none gives no tile.
 * @param t - Resolves a locale key.
 * @param values - The figure of each tile by its key; a tile with none shows an empty figure.
 * @returns The tiles in display order.
 */
export const reportStats = (
  stats: readonly SkeletonStat[] | undefined,
  t: ReportTranslate,
  values: Readonly<Record<string, ReportStatValue>> = {},
): ReportStat[] =>
  (stats ?? []).map(stat => ({ key: stat.key, label: t(stat.label), value: '', ...values[stat.key] }))

/** Key of the filter that holds the column picker of a query report; every other filter of such a report is a date range. */
export const REPORT_FIELDS_KEY = 'fields'

/**
 * Turns the filters of a query report's schema into its date ranges: every filter but the column picker, each sending
 * its own key as the prefix of its two query parameters.
 *
 * @param filters - The schema filters; none gives no range.
 * @param t - Resolves a locale key.
 * @returns The date ranges in display order.
 */
export const reportQueryRanges = (filters: readonly SkeletonField[] | undefined, t: ReportTranslate): ReportQueryRange[] =>
  (filters ?? [])
    .filter(field => field.key !== REPORT_FIELDS_KEY)
    .map(field => ({ key: field.key, label: field.label === undefined ? '' : t(field.label), param: field.key }))

/**
 * Translates the caption of one filter of a schema, for a filter the screen draws itself (a checkbox beside the table).
 *
 * @param schema - The report schema.
 * @param key - Key of the filter.
 * @param t - Resolves a locale key.
 * @returns The caption; empty when the schema has no such filter or the filter has no caption.
 */
export const reportFilterLabel = (schema: ReportSkeletonSchema, key: string, t: ReportTranslate): string => {
  const label = schema.filters?.find(field => field.key === key)?.label
  return label === undefined ? '' : t(label)
}

/**
 * Lists the keys of the columns of a schema table, in display order: the columns a query report shows before the
 * operator chooses others.
 *
 * @param table - The schema table; none gives no key.
 * @returns The column keys.
 */
export const reportColumnKeys = (table: SkeletonTable | undefined): string[] => (table?.columns ?? []).map(column => column.key)

/**
 * Picks the columns the advanced filters of a query report can target, in the order of the columns, so a filter
 * field is captioned exactly like its column.
 *
 * @param columns - Every column the report can show.
 * @param keys - Keys of the columns that can be filtered.
 * @returns The filter fields.
 */
export const reportFilterFields = (columns: readonly ReportColumn[], keys: readonly string[]): FieldOption[] =>
  columns.filter(column => keys.includes(column.key)).map(column => ({ value: column.key, label: column.label }))
