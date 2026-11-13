/**
 * Pinia store for admin order management: the paged list with a status filter, one order's
 * detail and the lifecycle commands (accept, retry of a failed line, closing a hand-finished
 * refund, cancel, delete, and approving or rejecting an order held for fraud review).
 *
 * Owns every request the order pages make and the sentence for a refusal, so the pages hold
 * no `catch`. A load puts its sentence in `error`; a command (accept, retry, cancel, review, delete) puts its
 * sentence in `actionError`. A retry also tracks which line is running and keeps a refusal per line.
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

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching items across all pages. */
  const totalCount = ref(0)

  /** True while a load is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused accept, retry, cancel, review or delete, null when none. */
  const actionError = ref<string | null>(null)

  /** Id of the order line whose retry is in flight, null when none, so only that line shows progress. */
  const retryingItemId = ref<number | null>(null)

  /** Sentence of the last refused retry by order line id, so each line shows its own refusal. */
  const lineErrors = ref<Record<number, string>>({})

  /** Lines the last accept or line retry could not fulfil; empty when every line was. */
  const unfulfilledLines = ref<OrderLineFailure[]>([])

  /** The lines the last accept or retry could not fulfil, named by domain or product, comma-separated. */
  const unfulfilledLineNames = computed(() =>
    unfulfilledLines.value.map((line) => line.domain ?? line.productName).join(', '),
  )

  /**
   * Loads one page of orders. The page and the status come from the list's address, so the store keeps
   * no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param status - Status to keep; null for all.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, status: OrderStatus | null): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.listOrders({ page, pageSize: pageSize.value, status }))
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
   * Accepts a pending order, keeps the lines it could not fulfil in `unfulfilledLines`, then
   * refreshes the current order detail.
   *
   * @param id - The order primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const acceptOrder = async (id: number): Promise<boolean> => {
    unfulfilledLines.value = []
    const result = await runAction(actionError, () => api.acceptOrder(id))
    if (result.ok) {
      unfulfilledLines.value = result.value.failedLines
      await fetchOne(id)
    }
    return result.ok
  }

  /**
   * Retries one failed order line, keeps the lines still failed in `unfulfilledLines`, then refreshes
   * the current order detail so the line shows its new state.
   *
   * @param id - The order primary key.
   * @param itemId - The failed line.
   * @returns True when the retry ran; false when refused (see `actionError`).
   */
  const retryOrderLine = async (id: number, itemId: number): Promise<boolean> => {
    unfulfilledLines.value = []
    const remaining = { ...lineErrors.value }
    delete remaining[itemId]
    lineErrors.value = remaining
    retryingItemId.value = itemId
    const result = await runAction(actionError, () => api.retryOrderLine(id, itemId))
    if (result.ok) {
      unfulfilledLines.value = result.value.failedLines
    } else if (actionError.value !== null) {
      lineErrors.value = { ...lineErrors.value, [itemId]: actionError.value }
    }
    retryingItemId.value = null
    // A refusal can mean the line changed under the reader (another retry won), so the detail is read again either way.
    await fetchOne(id)
    return result.ok
  }

  /**
   * Closes a partly refunded line as refunded, once staff gave back the rest of the cost by hand, then
   * refreshes the current order detail so the line and the order show their new state.
   *
   * @param id - The order primary key.
   * @param itemId - The partly refunded line.
   * @returns True when closed; false when refused (see `actionError`).
   */
  const closeLineRefund = async (id: number, itemId: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.closeOrderLineRefund(id, itemId))
    // A refusal can mean the line changed under the reader, so the detail is read again either way.
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
   * Approves an order held for fraud review, then refreshes the current order detail.
   *
   * @param id - The order primary key.
   * @returns True when approved; false when refused (see `actionError`).
   */
  const approveReview = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.approveOrderReview(id))
    // A refusal can mean another operator decided first, so the detail is read again either way.
    await fetchOne(id)
    return result.ok
  }

  /**
   * Rejects an order held for fraud review with the reason the client is mailed, then refreshes the current order detail.
   *
   * @param id - The order primary key.
   * @param reason - Why the order is refused.
   * @returns True when rejected; false when refused (see `actionError`).
   */
  const rejectReview = async (id: number, reason: string): Promise<boolean> => {
    const result = await runAction(actionError, () => api.rejectOrderReview(id, { reason }))
    // A refusal can mean another operator decided first, so the detail is read again either way.
    await fetchOne(id)
    return result.ok
  }

  /**
   * Deletes an order. The list screen reads the list again when the reader returns to it.
   *
   * @param id - The order primary key.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const deleteOrder = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.deleteOrder(id))
    return result.ok
  }

  return {
    orders,
    currentOrder,
    pageSize,
    totalCount,
    loading,
    error,
    actionError,
    retryingItemId,
    lineErrors,
    unfulfilledLines,
    unfulfilledLineNames,
    fetchAll,
    fetchOne,
    acceptOrder,
    retryOrderLine,
    closeLineRefund,
    cancelOrder,
    approveReview,
    rejectReview,
    deleteOrder,
  }
})
