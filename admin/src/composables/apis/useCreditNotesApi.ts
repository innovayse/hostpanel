/**
 * One function per endpoint on the credit note surface (`/api/billing/credit-notes` and `/api/billing/{id}/credit-notes`). No state
 * lives here, no error handling: a refusal rejects with an `ApiError` and the credit notes store turns it into a sentence.
 *
 * The caller is the credit notes store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { CreditNoteDetail, CreditNoteFilters, IssueCreditNotePayload } from '@/types/creditnote'
import type { CreditNote } from '@/types/creditnote'
import type { DownloadedFile } from '@/types/download'
import { HttpMethod } from '@/types/http'
import type { PaginatedList } from '@/types/paginatedlist'

/** Path of the credit note collection. */
const CREDIT_NOTES_PATH = `${ApiPath.Billing}/credit-notes`

/**
 * Endpoint functions for credit notes.
 *
 * @returns One typed function per credit note endpoint the admin panel uses.
 */
export const useCreditNotesApi = () => {
  /** Transport calls of the shared API module. */
  const { request, download } = useApi()

  /**
   * Lists one page of credit notes, newest first.
   *
   * @param filters - Invoice, client and status filters.
   * @param page - Page number (1-based).
   * @param pageSize - Notes per page.
   * @returns One page of credit notes.
   */
  const list = (filters: CreditNoteFilters, page: number, pageSize: number): Promise<PaginatedList<CreditNote>> =>
    request<PaginatedList<CreditNote>>(`${CREDIT_NOTES_PATH}${buildQuery({ ...filters, page, pageSize })}`)

  /**
   * Reads one credit note with its lines.
   *
   * @param id - The credit note.
   * @returns The note and its lines.
   */
  const get = (id: number | string): Promise<CreditNoteDetail> => request<CreditNoteDetail>(`${CREDIT_NOTES_PATH}/${id}`)

  /**
   * Issues a credit note against a paid invoice and gives its value back in the same operation.
   *
   * @param invoiceId - The paid invoice to credit.
   * @param payload - The lines, the method and the reason.
   * @returns The new note's id.
   */
  const issue = (invoiceId: number | string, payload: IssueCreditNotePayload): Promise<number> =>
    request<number>(`${ApiPath.Billing}/${invoiceId}/credit-notes`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Voids a credit note.
   *
   * @param id - The credit note.
   * @param reason - Why it is voided.
   * @returns Nothing.
   */
  const voidNote = (id: number | string, reason: string): Promise<void> =>
    request<void>(`${CREDIT_NOTES_PATH}/${id}/void`, { method: HttpMethod.Post, body: JSON.stringify({ reason }) })

  /**
   * Downloads a credit note's PDF as a file the page can save.
   *
   * @param id - The credit note.
   * @returns The PDF and the file name the API gave it.
   */
  const pdf = (id: number | string): Promise<DownloadedFile> => download(`${CREDIT_NOTES_PATH}/${id}/pdf`)

  return { list, get, issue, voidNote, pdf }
}
