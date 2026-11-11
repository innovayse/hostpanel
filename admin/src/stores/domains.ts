/**
 * Pinia store for admin domains management: the registration list, one domain's detail and
 * every change made to it (fields, registrar commands, DNS records, email forwarding).
 *
 * Owns every request the domain pages make and the sentence for a refusal, so the pages hold
 * no `catch`. A load puts its sentence in `error`; a mutation puts its sentence in `actionError`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useDomainsApi } from '@/composables/apis/useDomainsApi'
import { runAction } from '@/lib/runAction'
import type {
  DnsRecordPayload,
  DomainContactPayload,
  DomainDetail,
  DomainRegistration,
  ForwardingRulePayload,
  UpdateDomainPayload,
} from '@/types/domain'
import type { PaginatedList } from '@/types/paginatedlist'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

/** Loads and changes domain registrations. */
export const useDomainsStore = defineStore('domains', () => {
  const api = useDomainsApi()

  /** Loaded domain registrations list. */
  const domains = ref<DomainRegistration[]>([])

  /** Domains for a specific client. */
  const clientDomains = ref<DomainRegistration[]>([])

  /** Currently loaded domain detail. */
  const current = ref<DomainDetail | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching items. */
  const totalCount = ref(0)

  /** Search term for filtering domains by name. */
  const search = ref('')

  /** True while a load is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed mutation (save, registrar command, DNS or forwarding change), null when none. */
  const actionError = ref<string | null>(null)

  /** Per-field messages of the last refused save, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Loads the paged domain registrations for the current search.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listDomains({ page: page.value, pageSize: pageSize.value, search: search.value.trim() }),
    )
    loading.value = false
    if (result.ok) {
      domains.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Loads a client's domains into `clientDomains`.
   *
   * @param clientId - The client identifier to filter by.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchByClient = async (clientId: number | string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listDomains({ clientId, page: page.value, pageSize: pageSize.value }),
    )
    loading.value = false
    if (result.ok) {
      clientDomains.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Fetches one page of a client's domains without touching the shared list state.
   *
   * @param clientId - The client identifier to filter by.
   * @param pageNumber - Page, 1-based.
   * @param size - Items per page.
   * @returns The page, or null when refused (see `error`).
   */
  const fetchClientPage = async (
    clientId: number | string,
    pageNumber: number,
    size: number,
  ): Promise<PaginatedList<DomainRegistration> | null> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listDomains({ clientId, page: pageNumber, pageSize: size }),
    )
    loading.value = false
    return result.ok ? result.value : null
  }

  /**
   * Loads one domain's detail into `current`.
   *
   * @param id - Domain primary key.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchById = async (id: number | string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.getDomain(id))
    loading.value = false
    if (result.ok) {
      current.value = result.value
    }
    return result.ok
  }

  /**
   * Runs one mutating request, capturing a refusal into `actionError`.
   *
   * @param task - The request to run.
   * @returns True when the API accepted it; false when refused.
   */
  const mutate = async (task: () => Promise<unknown>): Promise<boolean> =>
    (await runAction(actionError, task, fieldErrors)).ok

  /**
   * Updates editable domain fields.
   *
   * @param id - Domain primary key.
   * @param data - Updated field values.
   * @returns True when accepted; false when refused (see `actionError` and `fieldErrors`).
   */
  const update = (id: number | string, data: UpdateDomainPayload): Promise<boolean> =>
    mutate(() => api.updateDomain(id, data))

  /**
   * Sends the register command to the registrar.
   *
   * @param id - Domain primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const register = (id: number | string): Promise<boolean> => mutate(() => api.registerDomain(id))

  /**
   * Renews a domain for additional years.
   *
   * @param id - Domain primary key.
   * @param years - Number of years to renew.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const renew = (id: number | string, years: number): Promise<boolean> =>
    mutate(() => api.renewDomain(id, years))

  /**
   * Toggles auto-renew for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether auto-renew should be enabled.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const setAutoRenew = (id: number | string, enabled: boolean): Promise<boolean> =>
    mutate(() => api.setAutoRenew(id, { enabled }))

  /**
   * Toggles WHOIS privacy for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether WHOIS privacy should be enabled.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const setWhoisPrivacy = (id: number | string, enabled: boolean): Promise<boolean> =>
    mutate(() => api.setWhoisPrivacy(id, { enabled }))

  /**
   * Toggles registrar lock for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether registrar lock should be enabled.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const setLock = (id: number | string, enabled: boolean): Promise<boolean> =>
    mutate(() => api.setLock(id, { enabled }))

  /**
   * Toggles DNS management for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether DNS management should be enabled.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const setDnsManagement = (id: number | string, enabled: boolean): Promise<boolean> =>
    mutate(() => api.setDnsManagement(id, { enabled }))

  /**
   * Toggles email forwarding for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether email forwarding should be enabled.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const setEmailForwarding = (id: number | string, enabled: boolean): Promise<boolean> =>
    mutate(() => api.setEmailForwarding(id, { enabled }))

  /**
   * Modifies WHOIS registrant contact details.
   *
   * @param id - Domain primary key.
   * @param contact - Updated contact details.
   * @returns True when accepted; false when refused (see `actionError` and `fieldErrors`).
   */
  const modifyContact = (id: number | string, contact: DomainContactPayload): Promise<boolean> =>
    mutate(() => api.modifyContact(id, contact))

  /**
   * Adds a DNS record to a domain.
   *
   * @param id - Domain primary key.
   * @param data - DNS record details.
   * @returns True when accepted; false when refused (see `actionError` and `fieldErrors`).
   */
  const addDnsRecord = (id: number | string, data: DnsRecordPayload): Promise<boolean> =>
    mutate(() => api.addDnsRecord(id, data))

  /**
   * Updates a DNS record.
   *
   * @param id - Domain primary key.
   * @param recordId - DNS record primary key.
   * @param data - Updated record details.
   * @returns True when accepted; false when refused (see `actionError` and `fieldErrors`).
   */
  const updateDnsRecord = (id: number | string, recordId: number, data: DnsRecordPayload): Promise<boolean> =>
    mutate(() => api.updateDnsRecord(id, recordId, data))

  /**
   * Deletes a DNS record.
   *
   * @param id - Domain primary key.
   * @param recordId - DNS record primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const deleteDnsRecord = (id: number | string, recordId: number): Promise<boolean> =>
    mutate(() => api.deleteDnsRecord(id, recordId))

  /**
   * Adds an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param source - Source alias.
   * @param destination - Destination email address.
   * @returns True when accepted; false when refused (see `actionError` and `fieldErrors`).
   */
  const addForwardingRule = (id: number | string, source: string, destination: string): Promise<boolean> =>
    mutate(() => api.addForwardingRule(id, { source, destination }))

  /**
   * Updates an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param ruleId - Forwarding rule primary key.
   * @param data - Updated rule details.
   * @returns True when accepted; false when refused (see `actionError` and `fieldErrors`).
   */
  const updateForwardingRule = (id: number | string, ruleId: number, data: ForwardingRulePayload): Promise<boolean> =>
    mutate(() => api.updateForwardingRule(id, ruleId, data))

  /**
   * Deletes an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param ruleId - Forwarding rule primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const deleteForwardingRule = (id: number | string, ruleId: number): Promise<boolean> =>
    mutate(() => api.deleteForwardingRule(id, ruleId))

  /**
   * Initiates an outgoing transfer and retrieves the EPP code.
   *
   * @param id - Domain primary key.
   * @returns The EPP authorization code, or null when refused (see `actionError`).
   */
  const getEppCode = async (id: number | string): Promise<string | null> => {
    const result = await runAction(actionError, () => api.initiateOutgoingTransfer(id))
    return result.ok ? result.value.eppCode : null
  }

  /**
   * Cancels a pending incoming transfer. A registrar without a cancel-transfer operation refuses
   * with the API's sentence for `REGISTRAR_OPERATION_UNSUPPORTED`.
   *
   * @param id - Domain primary key.
   * @returns True when accepted; false when refused (see `actionError`).
   */
  const cancelTransfer = (id: number | string): Promise<boolean> => mutate(() => api.cancelTransfer(id))

  return {
    domains, clientDomains, current, page, pageSize, totalCount, search, loading, error, actionError, fieldErrors,
    fetchAll, fetchByClient, fetchClientPage, fetchById, update, register, renew,
    setAutoRenew, setWhoisPrivacy, setLock, setDnsManagement, setEmailForwarding,
    modifyContact, getEppCode, cancelTransfer,
    addDnsRecord, updateDnsRecord, deleteDnsRecord,
    addForwardingRule, updateForwardingRule, deleteForwardingRule,
  }
})
