/**
 * Pinia store for the billing transaction ledger (all clients).
 *
 * It is the only caller of `useTransactionsApi` for this screen; a refusal becomes the API
 * sentence in `error` (and the per-field messages in `fieldErrors`).
 */

import { defineStore } from 'pinia'
import { reactive, ref } from 'vue'
import { useTransactionsApi } from '@/composables/apis/useTransactionsApi'
import { runAction } from '@/lib/runAction'
import type { CreateTransactionPayload, Transaction, TransactionFilters, UpdateTransactionPayload } from '@/types/transaction'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { CurrencyCode } from '@/types/currency'

/** Lists, creates, edits and deletes ledger transactions with their summary totals. */
export const useTransactionsStore = defineStore('billingTransactions', () => {
  const api = useTransactionsApi()

  /** Loaded transactions of the current page. */
  const transactions = ref<Transaction[]>([])

  /** Transaction open in the detail view. */
  const currentTransaction = ref<Transaction | null>(null)

  /** Total number of transactions across all pages. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, create or update, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused delete, null when none. */
  const actionError = ref<string | null>(null)

  /** Per-field messages of the last refused request, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Items per page. */
  const pageSize = ref(LEDGER_PAGE_SIZE)

  /** Financial summary totals of the rows the filters keep, in the base currency named by `currency`. */
  const summary = reactive({ totalIn: 0, totalOut: 0, totalFees: 0, balance: 0, currency: CurrencyCode.Usd as string })

  /**
   * Runs one request with the loading flag and the error state around it.
   *
   * @param task - The request to run.
   * @returns The outcome: the value on success, a failure whose sentence is in `error`.
   */
  const run = async <T>(task: () => Promise<T>) => {
    loading.value = true
    const result = await runAction(error, task, fieldErrors)
    loading.value = false
    return result
  }

  /**
   * Loads one transaction into `currentTransaction`.
   *
   * @param id - The transaction id.
   * @returns Promise that resolves when the transaction is loaded.
   */
  const fetchById = async (id: number): Promise<void> => {
    const result = await run(() => api.get(id))
    if (result.ok) {
      currentTransaction.value = result.value
    }
  }

  /**
   * Loads one page of the ledger, narrowed by the server, with the totals of the rows the filters keep. The page and the
   * filters come from the list's address, so the store keeps no copy of the filters.
   *
   * @param p - Page number (1-based).
   * @param filters - The search text and the client, direction, payment method, period and amount filters; a member left out does not narrow.
   * @param ps - Items per page.
   * @returns Promise that resolves when data is loaded.
   */
  const fetchAll = async (p = 1, filters: TransactionFilters = {}, ps = LEDGER_PAGE_SIZE): Promise<void> => {
    pageSize.value = ps
    const result = await run(() => api.list(p, ps, filters))
    if (result.ok) {
      transactions.value = result.value.transactions.items
      totalCount.value = result.value.transactions.totalCount
      summary.totalIn = result.value.totalIn
      summary.totalOut = result.value.totalOut
      summary.totalFees = result.value.totalFees
      summary.balance = result.value.balance
      summary.currency = result.value.currency
    }
  }

  /**
   * Creates a transaction. The list is not reloaded here: the store does not know the filters the screen shows, so the
   * screen reads the list again through its own address.
   *
   * @param payload - Transaction fields.
   * @returns True when created; false when refused (see `error`).
   */
  const create = async (payload: CreateTransactionPayload): Promise<boolean> => {
    const result = await run(() => api.create(payload))
    return result.ok
  }

  /**
   * Updates a transaction.
   *
   * @param id - The transaction id.
   * @param payload - Transaction fields.
   * @returns True when saved; false when refused (see `error`).
   */
  const update = async (id: number, payload: UpdateTransactionPayload): Promise<boolean> => {
    const result = await run(() => api.update(id, payload))
    return result.ok
  }

  /**
   * Deletes a transaction. The list is not reloaded here: the store does not know the filters the screen shows, so the
   * screen reads the list again through its own address.
   *
   * @param id - The transaction id.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    loading.value = true
    const result = await runAction(actionError, () => api.remove(id))
    loading.value = false
    return result.ok
  }

  return {
    transactions,
    currentTransaction,
    totalCount,
    summary,
    loading,
    error,
    actionError,
    fieldErrors,
    pageSize,
    fetchAll,
    fetchById,
    create,
    update,
    remove,
  }
})
