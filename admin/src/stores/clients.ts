/**
 * Pinia store for admin clients.
 *
 * Owns the paged client list, the open client, the profile save, contacts, export and the
 * pickers the other screens read. It is the only caller of `useClientsApi`; a refusal
 * becomes the API sentence in the matching error ref (`error`, `saveError`, `contactError`,
 * `exportError`, `externalAccountError`), so no view holds a `catch`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useClientsApi } from '@/composables/apis/useClientsApi'
import { toUpdateClientPayload } from '@/lib/clientPayload'
import { runAction } from '@/lib/runAction'
import type { AdminCreateClientPayload } from '@/types/admincreateclientpayload'
import type { ClientDetail, ClientFilters, ClientListItem } from '@/types/client'
import type { SetClientExternalAccountPayload } from '@/types/clientexternalaccount'
import type { ClientOption } from '@/types/clientoption'
import type { UserSearchHit } from '@/types/clientuser'
import type { UpdateClientPayload } from '@/types/updateclientpayload'
import type { CountryOption } from '@/types/countryoption'
import type { CurrencyOption } from '@/types/currencyoption'
import { DEFAULT_PAGE_SIZE, LOOKUP_PAGE_SIZE } from '@/lib/limits'

/** Number of suggestions a search box offers. */
const SUGGESTION_COUNT = 10

/** Lists, opens, creates and updates clients for the admin screens. */
export const useClientsStore = defineStore('clients', () => {
  const api = useClientsApi()

  /** Loaded clients list. */
  const clients = ref<ClientListItem[]>([])

  /** Total clients matching the current query. */
  const totalCount = ref(0)

  /** Current page number (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Currently viewed client detail, null until loaded. */
  const current = ref<ClientDetail | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** True while a profile save is in flight. */
  const saving = ref(false)

  /** Sentence for the last failed profile save, null when none. */
  const saveError = ref<string | null>(null)

  /** Per-field messages of the last refused profile save, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** True while an external account link save or clear is in flight. */
  const externalAccountSaving = ref(false)

  /** Sentence for the last refused external account link save or clear, null when none. */
  const externalAccountError = ref<string | null>(null)

  /** Per-field messages of the last refused external account link save, keyed by camelCase field name. */
  const externalAccountFieldErrors = ref<Record<string, string>>({})

  /** Sentence for the last failed contact add/update/remove, null when none. */
  const contactError = ref<string | null>(null)

  /** Sentence for the last failed data export, null when none. */
  const exportError = ref<string | null>(null)

  /** Cached list of countries from the reference endpoint. */
  const countries = ref<CountryOption[]>([])

  /** Cached list of currencies the operator enabled. */
  const currencies = ref<CurrencyOption[]>([])

  /**
   * Fetches a paginated, filtered client list.
   *
   * @param filters - Optional filter parameters.
   * @returns Promise that resolves when data is loaded.
   */
  const fetchAll = async (filters?: ClientFilters): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.list(page.value, pageSize.value, filters))
    loading.value = false
    if (result.ok) {
      clients.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Fetches a single client by id.
   *
   * @param id - The client identifier.
   * @returns Promise that resolves when data is loaded.
   */
  const fetchById = async (id: number | string): Promise<void> => {
    loading.value = true
    current.value = null
    // A refusal of the link save belongs to the client it was typed for, not to the next one opened.
    externalAccountError.value = null
    externalAccountFieldErrors.value = {}
    const result = await runAction(error, () => api.get(id))
    loading.value = false
    if (result.ok) {
      current.value = result.value
    }
  }

  /**
   * Creates a new client via the admin API, then refreshes the list.
   *
   * @param payload - Client creation payload.
   * @returns True when created; false when refused (see `saveError`, `fieldErrors`).
   */
  const createClient = async (payload: AdminCreateClientPayload): Promise<boolean> => {
    saving.value = true
    const result = await runAction(saveError, () => api.create(payload), fieldErrors)
    saving.value = false
    if (result.ok) {
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Replaces a client profile, address, notifications, settings and status (the API takes the whole record).
   *
   * @param id - The client identifier.
   * @param payload - The complete client record.
   * @returns True when saved and the detail refreshed; false when refused (see `saveError`, `fieldErrors`).
   */
  const updateClient = async (id: number | string, payload: UpdateClientPayload): Promise<boolean> => {
    saving.value = true
    const result = await runAction(
      saveError,
      async () => {
        await api.update(id, payload)
        // Re-read in place: `fetchById` blanks `current`, which swaps the whole client layout for
        // its loading state and unmounts the screen that is about to confirm the save.
        current.value = await api.get(id)
      },
      fieldErrors,
    )
    saving.value = false
    return result.ok
  }

  /**
   * Saves the internal notes of the open client and nothing else. The API replaces the whole
   * record, so the rest of it is sent back as it was loaded.
   *
   * @param id - The client identifier; must be the open client.
   * @param adminNotes - The new notes; an empty text clears them.
   * @returns True when saved; false when no such client is open or the API refused (see `saveError`).
   */
  const saveAdminNotes = async (id: number | string, adminNotes: string): Promise<boolean> => {
    const detail = current.value
    if (!detail || String(detail.id) !== String(id)) {
      return false
    }
    return updateClient(id, toUpdateClientPayload(detail, {
      preferences: { ...detail.preferences, adminNotes: adminNotes.trim() || null },
    }))
  }

  /**
   * Drops the API's message for one field of the external account link, once the operator edits it.
   *
   * @param field - The wire name of the field (see `ExternalAccountField`).
   * @returns Nothing; the field's message is removed from `externalAccountFieldErrors`.
   */
  const clearExternalAccountFieldError = (field: string): void => {
    const rest = { ...externalAccountFieldErrors.value }
    delete rest[field]
    externalAccountFieldErrors.value = rest
  }

  /**
   * Sets or clears the external account link of a client; the API is the only validator and
   * answers a pair another client holds with a conflict sentence.
   *
   * @param id - The client identifier.
   * @param payload - The provider and account id; both null clears the link.
   * @returns True when written and the detail refreshed; false when refused (see `externalAccountError`, `externalAccountFieldErrors`).
   */
  const saveExternalAccount = async (id: number | string, payload: SetClientExternalAccountPayload): Promise<boolean> => {
    externalAccountSaving.value = true
    const result = await runAction(
      externalAccountError,
      async () => {
        await api.setExternalAccount(id, payload)
        // Re-read in place, as the profile save does, so the card shows the link the API now holds.
        current.value = await api.get(id)
      },
      externalAccountFieldErrors,
    )
    externalAccountSaving.value = false
    return result.ok
  }

  /**
   * Reads the billing currency of one client without touching the opened client or the store error,
   * for a screen that prices something in it before the record exists (a new quote).
   *
   * @param id - The client identifier.
   * @returns The ISO 4217 code, or null when the client has none or could not be read.
   */
  const fetchClientCurrency = async (id: number): Promise<string | null> => {
    const result = await runAction(ref<string | null>(null), () => api.get(id))
    return result.ok ? (result.value.preferences.currency ?? null) : null
  }

  /**
   * Loads the clients a picker offers, with the display name already composed.
   *
   * @returns The options; an empty list when the call failed (see `error`).
   */
  const fetchClientOptions = async (): Promise<ClientOption[]> => {
    const result = await runAction(error, () => api.list(1, LOOKUP_PAGE_SIZE))
    if (!result.ok) {
      return []
    }
    return result.value.items.map(c => ({
      id: c.id,
      name: `${c.firstName} ${c.lastName}${c.companyName ? ` (${c.companyName})` : ''}`,
      email: c.email,
      status: c.status,
    }))
  }

  /**
   * Searches clients by name, email or id for a picker. A failure is not surfaced: the
   * suggestion list is just empty.
   *
   * @param term - Search text.
   * @returns Up to ten matches; empty when the call failed.
   */
  const searchClients = async (term: string): Promise<ClientListItem[]> => {
    /** Throwaway error ref: a failed suggestion lookup is deliberately silent. */
    const quiet = ref<string | null>(null)
    const result = await runAction(quiet, () => api.list(1, SUGGESTION_COUNT, { search: term }))
    return result.ok ? result.value.items : []
  }

  /**
   * Searches Identity users by a term (name or email). A failure is not surfaced: the
   * suggestion list is just empty.
   *
   * @param term - Search string.
   * @returns Matching users (max 10); empty when the call failed.
   */
  const searchUsers = async (term: string): Promise<UserSearchHit[]> => {
    /** Throwaway error ref: a failed suggestion lookup is deliberately silent. */
    const quiet = ref<string | null>(null)
    const result = await runAction(quiet, () => api.searchUsers(term, SUGGESTION_COUNT))
    return result.ok ? result.value.items : []
  }

  /**
   * Fetches the country list once; later calls are no-ops. A failure leaves the list empty
   * and is not shown, because the form still works with typed values.
   *
   * @returns Promise that resolves when countries are loaded.
   */
  const fetchCountries = async (): Promise<void> => {
    if (countries.value.length > 0) {
      return
    }
    /** Throwaway error ref: a missing reference list is deliberately silent. */
    const quiet = ref<string | null>(null)
    const result = await runAction(quiet, () => api.countries())
    countries.value = result.ok ? result.value : []
  }

  /**
   * Fetches the currencies a client may be billed in from the admin currency configuration,
   * so the picker only offers a currency the operator enabled. Later calls are no-ops.
   *
   * @returns Promise that resolves when currencies are loaded.
   */
  const fetchCurrencies = async (): Promise<void> => {
    if (currencies.value.length > 0) {
      return
    }
    /** Throwaway error ref: a missing reference list is deliberately silent. */
    const quiet = ref<string | null>(null)
    const result = await runAction(quiet, () => api.configuredCurrencies())
    currencies.value = result.ok
      ? result.value
          .filter(c => c.isEnabled)
          .map(c => ({ code: c.code, name: c.code, symbol: c.prefix || c.suffix }))
      : []
  }

  /**
   * Adds a new contact to a client, then refreshes the client.
   *
   * @param clientId - The client identifier.
   * @param payload - Contact creation data.
   * @returns True when added; false when refused (see `contactError`).
   */
  const addContact = async (clientId: number | string, payload: Record<string, unknown>): Promise<boolean> => {
    const result = await runAction(
      contactError,
      async () => {
        await api.addContact(clientId, payload)
        await fetchById(clientId)
      },
      fieldErrors,
    )
    return result.ok
  }

  /**
   * Updates a contact of a client, then refreshes the client.
   *
   * @param clientId - The client identifier.
   * @param contactId - The contact identifier.
   * @param payload - Updated contact data.
   * @returns True when updated; false when refused (see `contactError`).
   */
  const updateContact = async (
    clientId: number | string,
    contactId: number,
    payload: Record<string, unknown>,
  ): Promise<boolean> => {
    const result = await runAction(
      contactError,
      async () => {
        await api.updateContact(clientId, contactId, payload)
        await fetchById(clientId)
      },
      fieldErrors,
    )
    return result.ok
  }

  /**
   * Removes a contact from a client, then refreshes the client.
   *
   * @param clientId - The client identifier.
   * @param contactId - The contact identifier.
   * @returns True when removed; false when refused (see `contactError`).
   */
  const removeContact = async (clientId: number | string, contactId: number): Promise<boolean> => {
    const result = await runAction(contactError, async () => {
      await api.removeContact(clientId, contactId)
      await fetchById(clientId)
    })
    return result.ok
  }

  /**
   * Exports the selected sections of a client data.
   *
   * @param clientId - The client identifier.
   * @param fields - Section keys to include.
   * @returns The exported document, or null when refused (see `exportError`).
   */
  const exportClient = async (clientId: number | string, fields: string[]): Promise<object | null> => {
    const result = await runAction(exportError, () => api.exportData(clientId, fields))
    return result.ok ? result.value : null
  }

  return {
    clients,
    totalCount,
    page,
    pageSize,
    current,
    loading,
    error,
    saving,
    saveError,
    fieldErrors,
    externalAccountSaving,
    externalAccountError,
    externalAccountFieldErrors,
    contactError,
    exportError,
    countries,
    currencies,
    fetchAll,
    fetchById,
    createClient,
    updateClient,
    saveAdminNotes,
    saveExternalAccount,
    clearExternalAccountFieldError,
    fetchClientOptions,
    fetchClientCurrency,
    searchClients,
    searchUsers,
    fetchCountries,
    fetchCurrencies,
    addContact,
    updateContact,
    removeContact,
    exportClient,
  }
})
