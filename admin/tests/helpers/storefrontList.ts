/**
 * Helper for the storefront list screens' schema tests: the captions their table head draws.
 */
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from './i18n'
import { schemaCaptions } from './schemaTable'

/**
 * The English captions the head of a storefront list table draws, in order: the screen reader caption of the drag handle
 * column, then the captions of the schema (the actions column included).
 *
 * @param columns - The columns of the screen's list schema.
 * @returns The caption of each head cell that has text.
 */
export const storefrontHeadCaptions = (columns: readonly SkeletonColumn[]): string[] => [
  createTestI18n().global.t('storefront.list.reorder'),
  ...schemaCaptions(columns),
]
