import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Invoice, InvoiceItem, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin billing/invoices management.
 *
 * Handles global invoice listing, client-scoped invoice listing,
 * single invoice detail, and all invoice mutations.
 */
export const useBillingStore = defineStore('billing', () => {
  const { request } = useApi()

  /** Loaded invoices list (global). */
  const invoices = ref<Invoice[]>([])

  /** Total number of invoices across all pages (global). */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /** Currently loaded invoice detail. */
  const currentInvoice = ref<Invoice | null>(null)

  /** Client-scoped invoices list. */
  const clientInvoices = ref<Invoice[]>([])

  /** Total number of client-scoped invoices. */
  const clientInvoicesTotal = ref(0)

  /** Selected invoice IDs for bulk actions. */
  const selectedIds = ref<number[]>([])

  /**
   * Fetches all invoices from the backend with optional pagination and filters.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param status - Optional status filter.
   * @param from - Optional start date filter (ISO 8601).
   * @param to - Optional end date filter (ISO 8601).
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(
    page = 1,
    pageSize = 25,
    status = '',
    from = '',
    to = '',
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      let url = `/billing?page=${page}&pageSize=${pageSize}`
      if (status) url += `&status=${encodeURIComponent(status)}`
      if (from) url += `&from=${encodeURIComponent(from)}`
      if (to) url += `&to=${encodeURIComponent(to)}`
      const result = await request<PagedResult<Invoice>>(url)
      invoices.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load invoices.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single invoice by ID and sets currentInvoice.
   *
   * @param id - The invoice ID to fetch.
   * @returns Promise that resolves when the invoice is loaded.
   */
  async function fetchById(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentInvoice.value = await request<Invoice>(`/billing/${id}`)
    } catch {
      error.value = 'Failed to load invoice.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches invoices scoped to a specific client.
   *
   * @param clientId - The client ID.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param status - Optional status filter.
   * @param from - Optional start date filter.
   * @param to - Optional end date filter.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchClientInvoices(
    clientId: number | string,
    page = 1,
    pageSize = 25,
    status = '',
    from = '',
    to = '',
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      let url = `/billing/client/${clientId}?page=${page}&pageSize=${pageSize}`
      if (status) url += `&status=${encodeURIComponent(status)}`
      if (from) url += `&from=${encodeURIComponent(from)}`
      if (to) url += `&to=${encodeURIComponent(to)}`
      const result = await request<PagedResult<Invoice>>(url)
      clientInvoices.value = result.items
      clientInvoicesTotal.value = result.totalCount
    } catch {
      error.value = 'Failed to load client invoices.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new invoice for a client.
   *
   * @param clientId - The client to invoice.
   * @param dueDate - ISO 8601 due date string.
   * @param items - Line items to include.
   * @param isDraft - Whether to create as draft (true) or unpaid (false).
   * @returns The new invoice ID, or null on failure.
   */
  async function createInvoice(
    clientId: number,
    dueDate: string,
    items: Array<{ description: string; unitPrice: number; quantity: number }>,
    isDraft: boolean,
  ): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const result = await request<{ id: number }>('/billing', {
        method: 'POST',
        body: JSON.stringify({ clientId, dueDate, items, isDraft }),
      })
      return result.id
    } catch {
      error.value = 'Failed to create invoice.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates line items on an existing invoice.
   *
   * @param invoiceId - The invoice to update.
   * @param items - Updated line items array.
   * @returns Promise that resolves when the update completes.
   */
  async function updateItems(
    invoiceId: number,
    items: Array<{ id?: number; description: string; unitPrice: number; quantity: number }>,
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${invoiceId}/items`, {
        method: 'PUT',
        body: JSON.stringify({ items }),
      })
      await fetchById(invoiceId)
    } catch {
      error.value = 'Failed to update invoice items.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates invoice options (dates, payment method, tax rate).
   *
   * @param invoiceId - The invoice to update.
   * @param options - Options object with invoiceDate, dueDate, paymentMethod, taxRate.
   * @returns Promise that resolves when the update completes.
   */
  async function updateOptions(
    invoiceId: number,
    options: { invoiceDate: string; dueDate: string; paymentMethod: string; taxRate: number },
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${invoiceId}/options`, {
        method: 'PUT',
        body: JSON.stringify(options),
      })
      await fetchById(invoiceId)
    } catch {
      error.value = 'Failed to update invoice options.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates admin notes on an invoice.
   *
   * @param invoiceId - The invoice to update.
   * @param notes - Notes text.
   * @returns Promise that resolves when the update completes.
   */
  async function updateNotes(invoiceId: number, notes: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${invoiceId}/notes`, {
        method: 'PUT',
        body: JSON.stringify({ notes }),
      })
      await fetchById(invoiceId)
    } catch {
      error.value = 'Failed to update invoice notes.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Publishes a draft invoice (changes status from Draft to Unpaid).
   *
   * @param id - The invoice ID to publish.
   * @returns Promise that resolves when the action completes.
   */
  async function publishInvoice(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}/publish`, { method: 'POST' })
      await fetchById(id)
    } catch {
      error.value = 'Failed to publish invoice.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Attempts to capture payment via the configured gateway.
   *
   * @param id - The invoice ID.
   * @returns Promise that resolves when capture is attempted.
   */
  async function payInvoice(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}/pay`, {
        method: 'POST',
        body: JSON.stringify({ currency: 'USD' }),
      })
      await fetchById(id)
    } catch {
      error.value = 'Failed to capture payment.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Cancels an invoice.
   *
   * @param id - The invoice ID.
   * @returns Promise that resolves when cancelled.
   */
  async function cancelInvoice(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}/cancel`, { method: 'POST' })
      await fetchById(id)
    } catch {
      error.value = 'Failed to cancel invoice.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Records a payment against an invoice.
   *
   * @param id - The invoice ID.
   * @param payment - Payment details.
   * @returns Promise that resolves when the payment is recorded.
   */
  async function addPayment(
    id: number,
    payment: { date: string; gateway: string; transactionId: string; amount: number; fees?: number; notes?: string },
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}/payment`, {
        method: 'POST',
        body: JSON.stringify(payment),
      })
      await fetchById(id)
    } catch {
      error.value = 'Failed to add payment.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Applies a credit amount to an invoice.
   *
   * @param id - The invoice ID.
   * @param amount - Credit amount to apply.
   * @returns Promise that resolves when the credit is applied.
   */
  async function applyCredit(id: number, amount: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}/credit`, {
        method: 'POST',
        body: JSON.stringify({ amount }),
      })
      await fetchById(id)
    } catch {
      error.value = 'Failed to apply credit.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Removes a specific credit amount from an invoice.
   *
   * @param id - The invoice ID.
   * @param amount - The credit amount to remove.
   * @returns Promise that resolves when credit is removed.
   */
  async function removeCredit(id: number, amount: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}/credit/remove`, {
        method: 'POST',
        body: JSON.stringify({ amount }),
      })
      await fetchById(id)
    } catch {
      error.value = 'Failed to remove credit.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Processes a refund against a payment transaction.
   *
   * @param id - The invoice ID.
   * @param refund - Refund details including transaction ID, amount, gateway, and notes.
   * @returns Promise that resolves when the refund is processed.
   */
  async function refundPayment(
    id: number,
    refund: { transactionId: string; amount: number; refundType: string; gateway: string; refundTransactionId?: string; notes?: string },
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}/refund`, {
        method: 'POST',
        body: JSON.stringify(refund),
      })
      await fetchById(id)
    } catch {
      error.value = 'Failed to process refund.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Duplicates an existing invoice.
   *
   * @param id - The invoice ID to duplicate.
   * @returns The new invoice ID, or null on failure.
   */
  async function duplicateInvoice(id: number): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const result = await request<{ id: number }>(`/billing/${id}/duplicate`, { method: 'POST' })
      return result.id
    } catch {
      error.value = 'Failed to duplicate invoice.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Deletes an invoice by ID.
   *
   * @param id - The invoice ID to delete.
   * @returns Promise that resolves when the invoice is deleted.
   */
  async function deleteInvoice(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request(`/billing/${id}`, { method: 'DELETE' })
    } catch {
      error.value = 'Failed to delete invoice.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Performs a bulk action on multiple invoices.
   *
   * @param ids - Array of invoice IDs.
   * @param action - Bulk action name (e.g. "MarkPaid", "MarkUnpaid", "MarkCancelled", "Duplicate", "Delete").
   * @returns Promise that resolves when the bulk action completes.
   */
  async function bulkAction(ids: number[], action: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request('/billing/bulk', {
        method: 'POST',
        body: JSON.stringify({ invoiceIds: ids, action }),
      })
      selectedIds.value = []
    } catch {
      error.value = 'Failed to perform bulk action.'
    } finally {
      loading.value = false
    }
  }

  return {
    invoices,
    totalCount,
    loading,
    error,
    currentInvoice,
    clientInvoices,
    clientInvoicesTotal,
    selectedIds,
    fetchAll,
    fetchById,
    fetchClientInvoices,
    createInvoice,
    updateItems,
    updateOptions,
    updateNotes,
    publishInvoice,
    payInvoice,
    cancelInvoice,
    addPayment,
    applyCredit,
    removeCredit,
    refundPayment,
    duplicateInvoice,
    deleteInvoice,
    bulkAction,
  }
})
