/**
 * Pinia store for the signed-in client's credit notes (`pages/client/credit-notes`): the list of the notes issued to them and the
 * download or print of one as a PDF.
 *
 * A credit note is not invoice credit: invoice credit is balance the client spends on an invoice (`stores/credit.ts`), a credit note is
 * a numbered document that reverses part of an invoice that was already paid. The store owns the loaded notes, the in-flight flags and
 * the refusal sentences, so the page renders them and never wraps an API call in its own try/catch. Only the caller's own notes are
 * ever returned: the backend takes the client from the session.
 *
 * @module stores/creditnotes
 */

import { defineStore } from 'pinia'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { apiErrorMessage } from '~/lib/errorHandler'
import type { ClientCreditNote } from '~/types/clientcreditnote'
import { printBlob } from '~/utils/printBlob'
import { saveBlob } from '~/utils/saveBlob'

/**
 * Credit notes state for the client area.
 *
 * @returns The credit notes state and the actions that change it.
 */
export const useCreditNotesStore = defineStore('creditnotes', () => {
  const billing = useBillingApi()

  /** The notes on screen, newest first; empty until {@link load} or after a failed read. */
  const notes = ref<ClientCreditNote[]>([])

  /** Whether the notes are being read. */
  const loading = ref(false)

  /** The sentence for the last failed read; empty when there is none to show. */
  const error = ref('')

  /** The sentence for the last failed download; empty when there is none to show. */
  const downloadError = ref('')

  /**
   * Reads the notes issued to the client.
   *
   * @returns The notes; an empty list when they could not be read ({@link error} holds the sentence).
   */
  const load = async (): Promise<ClientCreditNote[]> => {
    loading.value = true
    error.value = ''
    try {
      notes.value = await billing.fetchCreditNotes()
    } catch (e: unknown) {
      notes.value = []
      error.value = apiErrorMessage(e)
    } finally {
      loading.value = false
    }
    return notes.value
  }

  /**
   * Fetches a note's PDF and hands it to `deliver`, the one flow behind downloading and printing it.
   *
   * @param note - The note whose PDF is wanted.
   * @param deliver - What to do with the PDF once it is read.
   * @returns Nothing; a refusal leaves its sentence in {@link downloadError}.
   */
  const deliverPdf = async (note: ClientCreditNote, deliver: (pdf: Blob) => void): Promise<void> => {
    downloadError.value = ''
    try {
      deliver(await billing.fetchCreditNotePdf(note.id))
    } catch (e: unknown) {
      downloadError.value = apiErrorMessage(e)
    }
  }

  /**
   * Downloads a note as a PDF and hands it to the browser as `credit-note-{number}.pdf`.
   *
   * @param note - The note to download.
   * @returns Nothing; a refusal leaves its sentence in {@link downloadError}.
   */
  const downloadPdf = (note: ClientCreditNote): Promise<void> => deliverPdf(note, (pdf) => saveBlob(pdf, `credit-note-${note.number}.pdf`))

  /**
   * Opens a note's PDF in the browser's print flow.
   *
   * @param note - The note to print.
   * @returns Nothing; a refusal leaves its sentence in {@link downloadError}.
   */
  const printPdf = (note: ClientCreditNote): Promise<void> => deliverPdf(note, printBlob)

  return { notes, loading, error, downloadError, load, downloadPdf, printPdf }
})
