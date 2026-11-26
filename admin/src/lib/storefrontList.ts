/**
 * What a storefront content list page reads from its skeleton schema.
 *
 * `StorefrontListTable` draws the drag handle column and the actions column itself and takes only the columns between
 * them, each with its caption already translated and the width, kind and card role the schema gives it. A page names
 * those columns once, in the columns of its `views/<Name>.schema.ts`, and resolves them here, so the head the table
 * draws and the head the skeleton draws are the same list.
 */
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'

/** Resolves a locale key to its text; the `t` of `useI18n`. */
export type StorefrontTranslate = (key: string) => string

/** One column of a storefront list between the handle and the actions: the schema's column with its caption translated. */
export interface StorefrontListColumn extends Pick<SkeletonColumn, 'key' | 'size' | 'kind' | 'width' | 'cardRole'> {
  /** The translated caption of the column header. */
  caption: string
}

/**
 * Resolves the columns of a storefront list schema that the page hands to the table.
 *
 * @param columns - The columns of the schema's table.
 * @param t - Resolves a locale key.
 * @returns The columns that have a caption and are not the actions column, in order, each with its caption translated; the
 * handle column has no caption of its own, and the actions caption is drawn by the table.
 */
export const storefrontListColumns = (columns: readonly SkeletonColumn[], t: StorefrontTranslate): StorefrontListColumn[] =>
  columns.flatMap((column) =>
    column.actions || column.label === undefined
      ? []
      : [{ key: column.key, caption: t(column.label), size: column.size, kind: column.kind, width: column.width, cardRole: column.cardRole }],
  )
