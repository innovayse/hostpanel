/**
 * What a storefront content list page reads from its skeleton schema.
 *
 * `StorefrontListTable` draws the drag handle column and the actions column itself and takes only the captions between
 * them, already translated. A page names those captions once, in the columns of its `views/<Name>.schema.ts`, and
 * resolves them here, so the head the table draws and the head the skeleton draws are the same list.
 */
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'

/** Resolves a locale key to its text; the `t` of `useI18n`. */
export type StorefrontTranslate = (key: string) => string

/**
 * Translates the captions of a storefront list schema that the page hands to the table.
 *
 * @param columns - The columns of the schema's table.
 * @param t - Resolves a locale key.
 * @returns The caption of each column that has one and is not the actions column, in order; the handle column has no
 * caption of its own, and the actions caption is drawn by the table.
 */
export const storefrontColumnCaptions = (columns: readonly SkeletonColumn[], t: StorefrontTranslate): string[] =>
  columns.flatMap((column) => (column.label === undefined || column.actions === true ? [] : [t(column.label)]))
