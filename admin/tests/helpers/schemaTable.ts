/**
 * Helpers for the list screens' schema tests: the captions a list schema promises for its table head, the captions a
 * mounted screen draws, and a request that never settles, so a screen stays in its first load.
 */
import type { VueWrapper } from '@vue/test-utils'
import { vi } from 'vitest'
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from './i18n'

/**
 * The English captions of the columns of a schema, in order: the label each column names, and the screen reader caption of
 * the actions column. A column with neither (a select-all or an icon column) draws no text and is left out.
 *
 * @param columns - The columns of a list schema.
 * @returns The caption each captioned column draws.
 */
export const schemaCaptions = (columns: readonly SkeletonColumn[]): string[] => {
  const { t } = createTestI18n().global
  return columns.flatMap((column) => {
    if (column.label !== undefined) {
      return [t(column.label)]
    }
    return column.actions === true ? [t('shared.actions')] : []
  })
}

/**
 * The captions of the table headers a mounted screen draws, in order; a header without text (the select-all checkbox) is
 * left out.
 *
 * @param wrapper - A mounted list screen.
 * @returns The text of every captioned header.
 */
export const renderedCaptions = (wrapper: VueWrapper): string[] =>
  wrapper.findAll('th').map((header) => header.text()).filter((text) => text !== '')

/**
 * Stubs the global `fetch` with a request that never settles, so a screen stays in its first load.
 *
 * @returns The mock, for asserting the requests made.
 */
export const stubPendingFetch = (): ReturnType<typeof vi.fn> => {
  const mock = vi.fn(() => new Promise<Response>(() => undefined))
  vi.stubGlobal('fetch', mock)
  return mock
}
