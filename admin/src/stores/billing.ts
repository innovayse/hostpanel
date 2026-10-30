/**
 * Pinia store for admin invoices.
 *
 * Owns the global invoice list, the client-scoped list, the open invoice and every invoice
 * mutation. It is the only caller of `useInvoicesApi`; a refusal becomes the API sentence in
 * `error` (and the per-field messages in `fieldErrors`), so no view holds a `catch`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useInvoicesApi } from '@/composables/apis/useInvoicesApi'
import { runAction } from '@/lib/runAction'
import type { DownloadedFile } from '@/types/download'
import type {
  CreateInvoicePayload,
  Invoice,
  InvoiceDetail,
  InvoiceItemInput,
  InvoiceItemUpdate,
  InvoiceOptionsPayload,
  InvoicePaymentPayload,
  InvoiceRefundPayload,
} from '@/types/invoice'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'

/** Loads, creates and changes invoices for the admin billing screens. */
export const useBillingStore = defineStore('billing', () => {
  const api = useInvoicesApi()

  /** Loaded invoices list (global). */
  const invoices = ref<Invoice[]>([])

  /** Total number of invoices across all pages (global). */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /** Per-field messages of the last refused request, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Currently loaded invoice detail. */
  const currentInvoice = ref<InvoiceDetail | null>(null)

  /** Client-scoped invoices list. */
  const clientInvoices = ref<Invoice[]>([])

  /** Total number of client-scoped invoices. */
  const clientInvoicesTotal = ref(0)

  /** Selected invoice ids for bulk actions. */
  const selectedIds = ref<number[]>([])

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
   * Runs a mutation of one invoice and reloads that invoice so the screen shows the result.
   *
   * @param id - The invoice id.
   * @param mutation - The request that changes the invoice.
   * @returns True when the change was accepted; false when refused (see `error`).
   */
  const mutateInvoice = async (id: number, mutation: () => Promise<unknown>): Promise<boolean> => {
    const result = await run(async () => {
      await mutation()
      currentInvoice.value = await api.get(id)
    })
    return result.ok
  }

  /**
   * Fetches all invoices with optional pagination and filters.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param status - Optional status filter.
   * @param from - Optional start date filter (ISO 8601).
   * @param to - Optional end date filter (ISO 8601).
   * @returns Promise that resolves when data is loaded.
   */
  const fetchAll = async (page = 1, pageSize = LEDGER_PAGE_SIZE, status = '', from = '', to = ''): Promise<void> => {
    const result = await run(() => api.list(page, pageSize, { status, from, to }))
    if (result.ok) {
      invoices.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Fetches a single invoice by id and sets `currentInvoice`.
   *
   * @param id - The invoice id to fetch.
   * @returns Promise that resolves when the invoice is loaded.
   */
  const fetchById = async (id: number): Promise<void> => {
    const result = await run(() => api.get(id))
    if (result.ok) {
      currentInvoice.value = result.value
    }
  }

  /**
   * Fetches invoices scoped to a specific client.
   *
   * @param clientId - The client id.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param status - Optional status filter.
   * @param from - Optional start date filter.
   * @param to - Optional end date filter.
   * @returns Promise that resolves when data is loaded.
   */
  const fetchClientInvoices = async (
    clientId: number | string,
    page = 1,
    pageSize = LEDGER_PAGE_SIZE,
    status = '',
    from = '',
    to = '',
  ): Promise<void> => {
    const result = await run(() => api.listByClient(clientId, page, pageSize, { status, from, to }))
    if (result.ok) {
      clientInvoices.value = result.value.items
      clientInvoicesTotal.value = result.value.totalCount
    }
  }

  /**
   * Creates a new invoice for a client.
   *
   * @param clientId - The client to invoice.
   * @param dueDate - ISO 8601 due date string.
   * @param items - Line items to include.
   * @param isDraft - Whether to create as draft (true) or unpaid (false).
   * @returns The new invoice id, or null when refused (see `error`).
   */
  const createInvoice = async (
    clientId: number,
    dueDate: string,
    items: InvoiceItemInput[],
    isDraft: boolean,
  ): Promise<number | null> => {
    const payload: CreateInvoicePayload = { clientId, dueDate, items, isDraft }
    const result = await run(() => api.create(payload))
    return result.ok ? result.value.id : null
  }

  /**
   * Replaces the line items of an invoice, then reloads it.
   *
   * @param invoiceId - The invoice to update.
   * @param items - The item changes to apply.
   * @returns True when saved; false when refused (see `error`).
   */
  const updateItems = (invoiceId: number, items: InvoiceItemUpdate[]): Promise<boolean> =>
    mutateInvoice(invoiceId, () => api.updateItems(invoiceId, items))

  /**
   * Updates invoice options (dates, payment method, tax rate), then reloads it.
   *
   * @param invoiceId - The invoice to update.
   * @param options - The new options.
   * @returns True when saved; false when refused (see `error`).
   */
  const updateOptions = (invoiceId: number, options: InvoiceOptionsPayload): Promise<boolean> =>
    mutateInvoice(invoiceId, () => api.updateOptions(invoiceId, options))

  /**
   * Updates the admin notes of an invoice, then reloads it.
   *
   * @param invoiceId - The invoice to update.
   * @param notes - Notes text.
   * @returns True when saved; false when refused (see `error`).
   */
  const updateNotes = (invoiceId: number, notes: string): Promise<boolean> =>
    mutateInvoice(invoiceId, () => api.updateNotes(invoiceId, notes))

  /**
   * Publishes a draft invoice (Draft to Unpaid), then reloads it.
   *
   * @param id - The invoice id to publish.
   * @returns True when published; false when refused (see `error`).
   */
  const publishInvoice = (id: number): Promise<boolean> => mutateInvoice(id, () => api.publish(id))

  /**
   * Cancels an invoice, then reloads it.
   *
   * @param id - The invoice id.
   * @returns True when cancelled; false when refused (see `error`).
   */
  const cancelInvoice = (id: number): Promise<boolean> => mutateInvoice(id, () => api.cancel(id))

  /**
   * Records a payment against an invoice, then reloads it.
   *
   * @param id - The invoice id.
   * @param payment - Payment details.
   * @returns True when recorded; false when refused (see `error`).
   */
  const addPayment = (id: number, payment: InvoicePaymentPayload): Promise<boolean> =>
    mutateInvoice(id, () => api.addPayment(id, payment))

  /**
   * Applies a credit amount to an invoice, then reloads it.
   *
   * @param id - The invoice id.
   * @param amount - Credit amount to apply.
   * @returns True when applied; false when refused (see `error`).
   */
  const applyCredit = (id: number, amount: number): Promise<boolean> =>
    mutateInvoice(id, () => api.applyCredit(id, amount))

  /**
   * Removes a credit amount from an invoice, then reloads it.
   *
   * @param id - The invoice id.
   * @param amount - The credit amount to remove.
   * @returns True when removed; false when refused (see `error`).
   */
  const removeCredit = (id: number, amount: number): Promise<boolean> =>
    mutateInvoice(id, () => api.removeCredit(id, amount))

  /**
   * Refunds a payment transaction, then reloads the invoice.
   *
   * @param id - The invoice id.
   * @param refund - Refund details.
   * @returns True when refunded; false when refused (see `error`).
   */
  const refundPayment = (id: number, refund: InvoiceRefundPayload): Promise<boolean> =>
    mutateInvoice(id, () => api.refund(id, refund))

  /**
   * Duplicates an existing invoice.
   *
   * @param id - The invoice id to duplicate.
   * @returns The new invoice id, or null when refused (see `error`).
   */
  const duplicateInvoice = async (id: number): Promise<number | null> => {
    const result = await run(() => api.duplicate(id))
    return result.ok ? result.value.id : null
  }

  /**
   * Deletes an invoice.
   *
   * @param id - The invoice id to delete.
   * @returns True when deleted; false when refused (see `error`).
   */
  const deleteInvoice = async (id: number): Promise<boolean> => {
    const result = await run(() => api.remove(id))
    return result.ok
  }

  /**
   * Applies one action to several invoices and clears the selection when it succeeded.
   *
   * @param ids - Invoice ids.
   * @param action - Bulk action name (e.g. `MarkPaid`, `MarkUnpaid`, `MarkCancelled`, `Duplicate`, `Delete`).
   * @returns True when applied; false when refused (see `error`).
   */
  const bulkAction = async (ids: number[], action: string): Promise<boolean> => {
    const result = await run(() => api.bulk(ids, action))
    if (result.ok) {
      selectedIds.value = []
    }
    return result.ok
  }

  /**
   * Downloads an invoice's PDF as a file.
   *
   * @param id - Invoice id.
   * @returns The PDF and its name, or null when refused (see `error`).
   */
  const downloadPdf = async (id: number): Promise<DownloadedFile | null> => {
    const result = await run(() => api.pdf(id))
    return result.ok ? result.value : null
  }

  return {
    invoices,
    totalCount,
    loading,
    error,
    fieldErrors,
    currentInvoice,
    clientInvoices,
    clientInvoicesTotal,
    selectedIds,
    fetchAll,
    fetchById,
    downloadPdf,
    fetchClientInvoices,
    createInvoice,
    updateItems,
    updateOptions,
    updateNotes,
    publishInvoice,
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
