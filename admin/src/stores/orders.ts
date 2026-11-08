/**
 * Pinia store for admin order management: the paged list with a status filter, one order's
 * detail and the lifecycle commands (accept, retry of a failed line, cancel, delete).
 *
 * Owns every request the order pages make and the sentence for a refusal, so the pages hold
 * no `catch`. A load puts its sentence in `error`; a command puts its sentence in `actionError`.
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { useOrdersApi } from '@/composables/apis/useOrdersApi'
import { runAction } from '@/lib/runAction'
import type { OrderDetail, OrderLineFailure, OrderListItem, OrderStatus } from '@/types/order'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

/** Loads and changes orders. */
export const useOrdersStore = defineStore('orders', () => {
  const api = useOrdersApi()

  /** Loaded orders list for the current page. */
  const orders = ref<OrderListItem[]>([])

  /** Currently viewed order detail. */
  const currentOrder = ref<OrderDetail | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching items across all pages. */
  const totalCount = ref(0)

  /** Optional status filter. */
  const statusFilter = ref<OrderStatus | null>(null)

  /** True while a load is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused accept, cancel or delete, null when none. */
  const actionError = ref<string | null>(null)

  /** Lines the last accept or line retry could not fulfil; empty when every line was. */
  const acceptFailures = ref<OrderLineFailure[]>([])

  /** The failed lines of the last accept, named by domain or product, comma-separated. */
  const acceptFailureNames = computed(() =>
    acceptFailures.value.map((line) => line.domain ?? line.productName).join(', '),
  )

  /**
   * Loads one page of orders for the active status filter.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listOrders({ page: page.value, pageSize: pageSize.value, status: statusFilter.value }),
    )
    loading.value = false
    if (result.ok) {
      orders.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Loads one order's detail into `currentOrder`.
   *
   * @param id - The order primary key.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchOne = async (id: number): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.getOrder(id))
    loading.value = false
    if (result.ok) {
      currentOrder.value = result.value
    }
    return result.ok
  }

  /**
   * Accepts a pending order, keeps the lines it could not fulfil in `acceptFailures`, then
   * refreshes the current order detail.
   *
   * @param id - The order primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const acceptOrder = async (id: number): Promise<boolean> => {
    acceptFailures.value = []
    const result = await runAction(actionError, () => api.acceptOrder(id))
    if (result.ok) {
      acceptFailures.value = result.value.failedLines
      await fetchOne(id)
    }
    return result.ok
  }

  /**
   * Retries one failed order line, keeps the lines still failed in `acceptFailures`, then refreshes
   * the current order detail so the line shows its new state.
   *
   * @param id - The order primary key.
   * @param itemId - The failed line.
   * @returns True when the retry ran; false when refused (see `actionError`).
   */
  const retryOrderLine = async (id: number, itemId: number): Promise<boolean> => {
    acceptFailures.value = []
    const result = await runAction(actionError, () => api.retryOrderLine(id, itemId))
    if (result.ok) {
      acceptFailures.value = result.value.failedLines
    }
    // A refusal can mean the line changed under the reader (another retry won), so the detail is read again either way.
    await fetchOne(id)
    return result.ok
  }

  /**
   * Cancels a pending order, then refreshes the current order detail.
   *
   * @param id - The order primary key.
   * @returns True when cancelled; false when refused (see `actionError`).
   */
  const cancelOrder = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.cancelOrder(id))
    if (result.ok) {
      await fetchOne(id)
    }
    return result.ok
  }

  /**
   * Deletes an order, then refreshes the list.
   *
   * @param id - The order primary key.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const deleteOrder = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.deleteOrder(id))
    if (result.ok) {
      await fetchAll()
    }
    return result.ok
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
    actionError,
    acceptFailures,
    acceptFailureNames,
    fetchAll,
    fetchOne,
    acceptOrder,
    retryOrderLine,
    cancelOrder,
    deleteOrder,
  }
})
