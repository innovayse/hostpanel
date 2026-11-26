/**
 * Pinia store for the registrar domain import: the page of domains a registrar plugin reports for its account, the paging
 * through it, and the import of one domain into a client.
 *
 * A registrar pages by cursor, never by number, so the store keeps the cursor that opened each page it has shown: Previous
 * goes back to a cursor it has already seen, Next follows the one the page carries. A load puts its sentence in `error`; an
 * import puts its sentence in `actionError`.
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { useRegistrarDomainsApi } from '@/composables/apis/useRegistrarDomainsApi'
import { runAction } from '@/lib/runAction'
import type { ImportRegistrarDomainPayload, RegistrarDomain } from '@/types/registrardomain'

/** Loads the registrar's domains and imports them. */
export const useRegistrarDomainsStore = defineStore('registrarDomains', () => {
  const api = useRegistrarDomainsApi()

  /** The domains of the page on screen. */
  const domains = ref<RegistrarDomain[]>([])

  /** The cursor of the page after the one on screen, null on the last page. */
  const nextCursor = ref<string | null>(null)

  /** The cursor that opened each page shown so far; the first page has none. */
  const cursors = ref<(string | null)[]>([null])

  /** The index in `cursors` of the page on screen. */
  const position = ref(0)

  /** True while a page is loading. */
  const loading = ref(false)

  /** Sentence for the last refused load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused import, null when none. */
  const actionError = ref<string | null>(null)

  /** Per-field messages of the last refused import, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** The 1-based number of the page on screen. */
  const pageNumber = computed<number>(() => position.value + 1)

  /** Whether a page follows the one on screen. */
  const hasNext = computed<boolean>(() => nextCursor.value !== null)

  /** Whether a page precedes the one on screen. */
  const hasPrevious = computed<boolean>(() => position.value > 0)

  /**
   * Reads the page a cursor opens and shows it; a refusal leaves the page on screen as it was.
   *
   * @param module - The id of the registrar plugin.
   * @param cursor - The cursor of the wanted page, null for the first.
   * @returns True when the page is shown; false when refused (see `error`).
   */
  const show = async (module: string, cursor: string | null): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.listDomains(module, cursor))
    loading.value = false
    if (result.ok) {
      domains.value = result.value.domains
      nextCursor.value = result.value.nextCursor
    }
    return result.ok
  }

  /**
   * Starts again at the registrar's first page.
   *
   * @param module - The id of the registrar plugin.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchFirst = async (module: string): Promise<boolean> => {
    cursors.value = [null]
    position.value = 0
    domains.value = []
    nextCursor.value = null
    return show(module, null)
  }

  /**
   * Shows the page after the one on screen.
   *
   * @param module - The id of the registrar plugin.
   * @returns True when shown; false when there is no next page or the registrar refused (see `error`).
   */
  const fetchNext = async (module: string): Promise<boolean> => {
    const cursor = nextCursor.value
    if (cursor === null) {
      return false
    }
    const shown = await show(module, cursor)
    if (shown) {
      cursors.value = [...cursors.value.slice(0, position.value + 1), cursor]
      position.value += 1
    }
    return shown
  }

  /**
   * Shows the page before the one on screen.
   *
   * @param module - The id of the registrar plugin.
   * @returns True when shown; false when this is the first page or the registrar refused (see `error`).
   */
  const fetchPrevious = async (module: string): Promise<boolean> => {
    // The first page is position 0, whose predecessor index reads as undefined: no earlier cursor, so nothing to show.
    const cursor = cursors.value[position.value - 1]
    if (cursor === undefined) {
      return false
    }
    const shown = await show(module, cursor)
    if (shown) {
      position.value -= 1
    }
    return shown
  }

  /**
   * Imports one domain into a client and marks it as in the panel on the page on screen.
   *
   * @param module - The id of the registrar plugin that holds the domain.
   * @param payload - The client and the domain name.
   * @returns True when imported; false when refused (see `actionError` and `fieldErrors`).
   */
  const importDomain = async (module: string, payload: ImportRegistrarDomainPayload): Promise<boolean> => {
    const result = await runAction(actionError, () => api.importDomain(module, payload), fieldErrors)
    if (result.ok) {
      domains.value = domains.value.map(domain =>
        domain.name === payload.domainName ? { ...domain, inPanel: true } : domain,
      )
    }
    return result.ok
  }

  /**
   * Forgets the pages and the sentences, for the next time the dialog opens.
   *
   * @returns Nothing; the store is back to its empty state.
   */
  const reset = (): void => {
    domains.value = []
    nextCursor.value = null
    cursors.value = [null]
    position.value = 0
    error.value = null
    actionError.value = null
    fieldErrors.value = {}
  }

  return {
    domains, loading, error, actionError, fieldErrors, pageNumber, hasNext, hasPrevious,
    fetchFirst, fetchNext, fetchPrevious, importDomain, reset,
  }
})
