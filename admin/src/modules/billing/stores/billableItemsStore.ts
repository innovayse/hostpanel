import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { BillableItem, PagedResult } from '../../../types/models'

export const useBillableItemsStore = defineStore('billableItems', () => {
  const { request } = useApi()

  const items = ref<BillableItem[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchAll(type?: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ page: '1', pageSize: '100' })
      if (type) params.append('type', type)
      const result = await request<PagedResult<BillableItem>>(`/billing/billable-items?${params.toString()}`)
      items.value = result.items
    } catch {
      error.value = 'Failed to load billable items.'
    } finally {
      loading.value = false
    }
  }

  async function create(item: {
    clientId: number
    description: string
    amount: number
    currency: string
    type: string
    recurringPeriod?: string
    nextDueDate?: string
  }): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request('/billing/billable-items', {
        method: 'POST',
        body: JSON.stringify(item),
      })
      await fetchAll()
    } catch {
      error.value = 'Failed to create billable item.'
    } finally {
      loading.value = false
    }
  }

  async function removeItem(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/billable-items/${id}`, {
        method: 'DELETE',
      })
      await fetchAll()
    } catch {
      error.value = 'Failed to delete billable item.'
    } finally {
      loading.value = false
    }
  }

  return { items, loading, error, fetchAll, create, removeItem }
})
