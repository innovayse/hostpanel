/**
 * Shapes shared by every storefront content type: the translation map and the reorder pair.
 */
import type { MessageLanguage } from '@/locales/messages'

/** Per-locale text of one entry; a locale the operator has not written is absent. */
export type StorefrontTranslations<T> = Partial<Record<MessageLanguage, T>>

/** What the last successful action of a storefront list page did, shown as its outcome line. */
export enum StorefrontListOutcome {
  /** The staged entry was deleted. */
  Deleted = 'deleted',
  /** The new display order was saved. */
  OrderSaved = 'orderSaved',
}

/** One entry's new position in the display order. */
export interface StorefrontOrderItem {
  /** Entry ID. */
  id: number
  /** New display order. */
  sortOrder: number
}

/** The part of an entry the admin list and form machinery relies on. */
export interface StorefrontEntity {
  /** Entry ID. */
  id: number
  /** Display order. */
  sortOrder: number
}

/** Test ids of one storefront list page; the row-level ones are prefixes that take `-<id>`. */
export interface StorefrontListTestIds {
  /** The page root. */
  view: string
  /** The Add link. */
  add: string
  /** The load-error banner. */
  error: string
  /** The outcome line of a delete or reorder: the success sentence or the API's refusal. */
  actionError: string
  /** The saving-order notice. */
  savingOrder: string
  /** Prefix of a table row. */
  row: string
  /** Prefix of a row's actions menu trigger. */
  actions: string
  /** Prefix of a row's edit link. */
  edit: string
  /** Prefix of a row's delete button. */
  delete: string
  /** Prefix of a row's move-up button. */
  moveUp: string
  /** Prefix of a row's move-down button. */
  moveDown: string
}

/**
 * Builds the test ids of one storefront list page. The page ids use the plural prefix
 * (`faqs-view`); the row ids use its singular (`faq-row`), taken by dropping the trailing `s`.
 *
 * @param prefix - The plural page prefix, for example `faqs`.
 * @returns The ids the list table renders.
 */
export const storefrontTestIds = (prefix: string): StorefrontListTestIds => {
  const row = prefix.endsWith('s') ? prefix.slice(0, -1) : prefix
  return {
    view: `${prefix}-view`,
    add: `${prefix}-add`,
    error: `${prefix}-error`,
    actionError: `${prefix}-action-error`,
    savingOrder: `${prefix}-saving-order`,
    row: `${row}-row`,
    actions: `${row}-actions`,
    edit: `${row}-edit`,
    delete: `${row}-delete`,
    moveUp: `${row}-move-up`,
    moveDown: `${row}-move-down`,
  }
}
