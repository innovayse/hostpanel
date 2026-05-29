import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { OrderListItem, OrderDetail, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin order management.
 *
 * Provides paginated order listing with status filtering,
 * single order detail fetching, and order lifecycle actions.
 */
export const useOrdersStore = defineStore('orders', () => {
  const { request } = useApi()

  /** Loaded orders list for the current page. */
  const orders = ref<OrderListItem[]>([])

  /** Currently viewed order detail. */
  const currentOrder = ref<OrderDetail | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Total number of matching items across all pages. */
  const totalCount = ref(0)

  /** Optional status filter. */
  const statusFilter = ref<string | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches paginated orders from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      let url = `/orders?page=${page.value}&pageSize=${pageSize.value}`
      if (statusFilter.value) {
        url += `&status=${statusFilter.value}`
      }
      const result = await request<PagedResult<OrderListItem>>(url)
      orders.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load orders.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single order by ID.
   *
   * @param id - The order primary key.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchOne(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentOrder.value = await request<OrderDetail>(`/orders/${id}`)
    } catch {
      error.value = 'Failed to load order details.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Accepts a pending order, then refreshes the current order detail.
   *
   * @param id - The order primary key.
   * @returns Promise that resolves when the action completes.
   */
  async function acceptOrder(id: number): Promise<void> {
    await request(`/orders/${id}/accept`, { method: 'POST' })
    await fetchOne(id)
  }

  /**
   * Cancels a pending order, then refreshes the current order detail.
   *
   * @param id - The order primary key.
   * @returns Promise that resolves when the action completes.
   */
  async function cancelOrder(id: number): Promise<void> {
    await request(`/orders/${id}/cancel`, { method: 'POST' })
    await fetchOne(id)
  }

  /**
   * Deletes an order, then refreshes the list.
   *
   * @param id - The order primary key.
   * @returns Promise that resolves when the action completes.
   */
  async function deleteOrder(id: number): Promise<void> {
    await request(`/orders/${id}`, { method: 'DELETE' })
    await fetchAll()
  }

  return {
    orders,
    currentOrder,
    page,
    pageSize,
    totalCount,
    statusFilter,
    loading,
    error,
    fetchAll,
    fetchOne,
    acceptOrder,
    cancelOrder,
    deleteOrder,
  }
})
