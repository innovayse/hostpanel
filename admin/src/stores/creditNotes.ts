/**
 * Pinia store for credit notes: the list (all, or those of one invoice), one note with its lines, issuing, voiding and the PDF.
 *
 * It is the only caller of `useCreditNotesApi`; a refusal becomes the API sentence in `error` (and the per-field messages in
 * `fieldErrors`). A credit note is not an invoice credit: invoice credit is balance spent on an invoice, a credit note reverses
 * part of an invoice that was already paid.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useCreditNotesApi } from '@/composables/apis/useCreditNotesApi'
import { runAction } from '@/lib/runAction'
import type { CreditNote, CreditNoteDetail, CreditNoteFilters, IssueCreditNotePayload } from '@/types/creditnote'
import type { DownloadedFile } from '@/types/download'

/** Loads, issues and voids credit notes. */
export const useCreditNotesStore = defineStore('creditNotes', () => {
  /** The credit note endpoints; this store is their only caller. */
  const api = useCreditNotesApi()

  /** Notes of the current page. */
  const notes = ref<CreditNote[]>([])

  /** Total number of notes for the current filters, across all pages. */
  const totalCount = ref(0)

  /** The note on its page; null until loaded or after a refused load. */
  const detail = ref<CreditNoteDetail | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /** Per-field messages of the last refused issue. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Loads one page of credit notes.
   *
   * @param filters - Invoice, client and status filters.
   * @param page - Page number (1-based).
   * @param pageSize - Notes per page.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchPage = async (filters: CreditNoteFilters, page: number, pageSize: number): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.list(filters, page, pageSize))
    loading.value = false
    if (result.ok) {
      notes.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Loads one credit note with its lines.
   *
   * @param id - The credit note.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchDetail = async (id: number | string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.get(id))
    loading.value = false
    detail.value = result.ok ? result.value : null
    return result.ok
  }

  /**
   * Issues a credit note against a paid invoice.
   *
   * @param invoiceId - The paid invoice to credit.
   * @param payload - The lines, the method and the reason.
   * @returns The new note's id, or null when refused (see `error` and `fieldErrors`).
   */
  const issue = async (invoiceId: number | string, payload: IssueCreditNotePayload): Promise<number | null> => {
    const result = await runAction(error, () => api.issue(invoiceId, payload), fieldErrors)
    return result.ok ? result.value : null
  }

  /**
   * Voids a credit note and reloads it, so the page shows the new state.
   *
   * @param id - The credit note.
   * @param reason - Why it is voided.
   * @returns True when voided; false when refused (see `error`).
   */
  const voidNote = async (id: number | string, reason: string): Promise<boolean> => {
    const result = await runAction(error, () => api.voidNote(id, reason))
    if (!result.ok) {
      return false
    }
    return fetchDetail(id)
  }

  /**
   * Downloads a credit note's PDF.
   *
   * @param id - The credit note.
   * @returns The PDF and its name, or null when refused (see `error`).
   */
  const downloadPdf = async (id: number | string): Promise<DownloadedFile | null> => {
    const result = await runAction(error, () => api.pdf(id))
    return result.ok ? result.value : null
  }

  return { notes, totalCount, detail, loading, error, fieldErrors, fetchPage, fetchDetail, issue, voidNote, downloadPdf }
})
