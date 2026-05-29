import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Quote, PagedResult } from '../../../types/models'

export const useQuotesStore = defineStore('quotes', () => {
  const { request } = useApi()

  const quotes = ref<Quote[]>([])
  const currentQuote = ref<Quote | null>(null)
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
      const result = await request<PagedResult<Quote>>(`/billing/quotes?${query}`)
      quotes.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load quotes.'
    } finally {
      loading.value = false
    }
  }

  async function getById(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentQuote.value = await request<Quote>(`/billing/quotes/${id}`)
    } catch {
      error.value = 'Failed to load quote.'
    } finally {
      loading.value = false
    }
  }

  async function create(quote: {
    clientId: number
    subject: string
    expiryDate: string
    notes?: string
    items: Array<{
      description: string
      unitPrice: number
      quantity: number
    }>
  }): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request('/billing/quotes', {
        method: 'POST',
        body: JSON.stringify(quote),
      })
      await fetchAll()
    } catch {
      error.value = 'Failed to create quote.'
    } finally {
      loading.value = false
    }
  }

  return { quotes, currentQuote, totalCount, loading, error, page, pageSize, fetchAll, getById, create }
})
