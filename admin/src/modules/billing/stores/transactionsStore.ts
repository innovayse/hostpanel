import { defineStore } from 'pinia'
import { ref, reactive } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Transaction, TransactionsResult } from '../../../types/models'

export const useTransactionsStore = defineStore('billing-transactions', () => {
  const { request } = useApi()

  const transactions = ref<Transaction[]>([])
  const totalCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const page = ref(1)
  const pageSize = ref(25)
  const summary = reactive({ totalIn: 0, totalOut: 0, totalFees: 0, balance: 0 })

  async function fetchAll(p = 1, ps = 25): Promise<void> {
    page.value = p
    pageSize.value = ps
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams({ page: String(p), pageSize: String(ps) }).toString()
      const result = await request<TransactionsResult>(`/transactions?${query}`)
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
    addToCredit: boolean
  }): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await request('/transactions', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      await fetchAll(page.value, pageSize.value)
      return true
    } catch {
      error.value = 'Failed to create transaction.'
      return false
    } finally {
      loading.value = false
    }
  }

  async function remove(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/transactions/${id}`, { method: 'DELETE' })
      await fetchAll(page.value, pageSize.value)
    } catch {
      error.value = 'Failed to delete transaction.'
    } finally {
      loading.value = false
    }
  }

  return { transactions, totalCount, summary, loading, error, page, pageSize, fetchAll, create, remove }
})
