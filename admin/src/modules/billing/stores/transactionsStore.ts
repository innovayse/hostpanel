import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Transaction, PagedResult } from '../../../types/models'

export const useTransactionsStore = defineStore('transactions', () => {
  const { request } = useApi()

  const transactions = ref<Transaction[]>([])
  const currentTransaction = ref<Transaction | null>(null)
  const totalCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const page = ref(1)
  const pageSize = ref(20)

  async function fetchAll(p = 1, ps = 20): Promise<void> {
    page.value = p
    pageSize.value = ps
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams({ page: String(p), pageSize: String(ps) }).toString()
      const result = await request<PagedResult<Transaction>>(`/billing/transactions?${query}`)
      transactions.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load transactions.'
    } finally {
      loading.value = false
    }
  }

  async function fetchById(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentTransaction.value = await request<Transaction>(`/billing/transactions/${id}`)
    } catch {
      error.value = 'Failed to load transaction.'
    } finally {
      loading.value = false
    }
  }

  async function create(data: {
    clientId: number
    description: string
    amount: number
    fees: number
    currency: string
    gateway?: string
    transactionId?: string
    type: 'Credit' | 'Debit'
  }): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request('/billing/transactions', {
        method: 'POST',
        body: JSON.stringify(data),
      })
      await fetchAll()
    } catch {
      error.value = 'Failed to create transaction.'
    } finally {
      loading.value = false
    }
  }

  async function update(id: number, data: {
    clientId: number
    description: string
    amount: number
    fees: number
    currency: string
    gateway?: string
    transactionId?: string
    type: 'Credit' | 'Debit'
  }): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/transactions/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      })
      await fetchAll()
    } catch {
      error.value = 'Failed to update transaction.'
    } finally {
      loading.value = false
    }
  }

  return { transactions, currentTransaction, totalCount, loading, error, page, pageSize, fetchAll, fetchById, create, update }
})
