import { defineStore } from 'pinia'
import { ref, reactive } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Transaction, TransactionsResult } from '../../../types/models'

/**
 * Pinia store for transaction management.
 *
 * Handles listing, creating, and deleting transactions for a client.
 * Maintains summary totals alongside the paginated transaction list.
 */
export const useTransactionsStore = defineStore('transactions', () => {
  const { request } = useApi()

  /** Loaded transactions for the current client. */
  const transactions = ref<Transaction[]>([])

  /** Total number of transactions across all pages. */
  const totalCount = ref(0)

  /** Financial summary totals for the client. */
  const summary = reactive({
    /** Sum of all AmountIn values. */
    totalIn: 0,
    /** Sum of all AmountOut values. */
    totalOut: 0,
    /** Sum of all Fees values. */
    totalFees: 0,
    /** Calculated balance: TotalIn - TotalOut - TotalFees. */
    balance: 0,
  })

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches transactions for a specific client with pagination.
   *
   * @param clientId - The client ID.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchByClient(
    clientId: number | string,
    page = 1,
    pageSize = 25,
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<TransactionsResult>(
        `/transactions/client/${clientId}?page=${page}&pageSize=${pageSize}`,
      )
      transactions.value = result.transactions.items
      totalCount.value = result.transactions.totalCount
      summary.totalIn = result.totalIn
      summary.totalOut = result.totalOut
      summary.totalFees = result.totalFees
      summary.balance = result.balance
    } catch {
      error.value = 'Failed to load transactions.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new transaction.
   *
   * @param payload - Transaction data to submit.
   * @returns True if creation succeeded, false otherwise.
   */
  async function create(payload: {
    clientId: number
    date: string
    description: string
    transactionId: string
    invoiceId: number | null
    paymentMethod: string
    amountIn: number
    amountOut: number
    fees: number
    addedToCredit: boolean
  }): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await request('/transactions', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      return true
    } catch {
      error.value = 'Failed to create transaction.'
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * Deletes a transaction by ID.
   *
   * @param id - The transaction ID to delete.
   * @returns Promise that resolves when deletion completes.
   */
  async function remove(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/transactions/${id}`, { method: 'DELETE' })
    } catch {
      error.value = 'Failed to delete transaction.'
    } finally {
      loading.value = false
    }
  }

  return {
    transactions,
    totalCount,
    summary,
    loading,
    error,
    fetchByClient,
    create,
    remove,
  }
})
