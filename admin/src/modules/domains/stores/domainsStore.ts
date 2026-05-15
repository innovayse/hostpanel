import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { DomainRegistration, DomainDetail, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin domains management.
 */
export const useDomainsStore = defineStore('domains', () => {
  const { request } = useApi()

  /** Loaded domain registrations list. */
  const domains = ref<DomainRegistration[]>([])

  /** Domains for a specific client. */
  const clientDomains = ref<DomainRegistration[]>([])

  /** Currently loaded domain detail. */
  const current = ref<DomainDetail | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Total number of matching items. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches paginated domain registrations from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<DomainRegistration>>(`/domains?page=${page.value}&pageSize=${pageSize.value}`)
      domains.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load domains.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches domains for a specific client.
   *
   * @param clientId - The client identifier to filter by.
   */
  async function fetchByClient(clientId: number | string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<DomainRegistration>>(
        `/domains?clientId=${clientId}&page=${page.value}&pageSize=${pageSize.value}`,
      )
      clientDomains.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load client domains.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single domain by ID.
   *
   * @param id - Domain primary key.
   */
  async function fetchById(id: number | string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      current.value = await request<DomainDetail>(`/domains/${id}`)
    } catch {
      error.value = 'Failed to load domain details.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates editable domain fields.
   *
   * @param id - Domain primary key.
   * @param data - Updated field values.
   */
  async function update(id: number | string, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}`, { method: 'PUT', body: JSON.stringify(data) })
  }

  /**
   * Renews a domain for additional years.
   *
   * @param id - Domain primary key.
   * @param years - Number of years to renew.
   */
  async function renew(id: number | string, years: number): Promise<void> {
    await request(`/domains/${id}/renew`, { method: 'POST', body: JSON.stringify({ years }) })
  }

  /**
   * Toggles auto-renew for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether auto-renew should be enabled.
   */
  async function setAutoRenew(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/auto-renew`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles WHOIS privacy for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether WHOIS privacy should be enabled.
   */
  async function setWhoisPrivacy(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/whois-privacy`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles registrar lock for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether registrar lock should be enabled.
   */
  async function setLock(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/lock`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles DNS management for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether DNS management should be enabled.
   */
  async function setDnsManagement(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/dns-management`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles email forwarding for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether email forwarding should be enabled.
   */
  async function setEmailForwarding(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/email-forwarding-toggle`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Modifies WHOIS registrant contact details.
   *
   * @param id - Domain primary key.
   * @param contact - Updated contact details.
   */
  async function modifyContact(id: number | string, contact: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/modify-contact`, { method: 'POST', body: JSON.stringify(contact) })
  }

  /**
   * Initiates an outgoing transfer and retrieves the EPP code.
   *
   * @param id - Domain primary key.
   * @returns The EPP authorization code.
   */
  async function getEppCode(id: number | string): Promise<string> {
    return await request<string>(`/domains/${id}/initiate-outgoing-transfer`, { method: 'POST' })
  }

  /**
   * Adds a DNS record to a domain.
   *
   * @param id - Domain primary key.
   * @param data - DNS record details.
   */
  async function addDnsRecord(id: number | string, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/dns`, { method: 'POST', body: JSON.stringify(data) })
  }

  /**
   * Updates a DNS record.
   *
   * @param id - Domain primary key.
   * @param recordId - DNS record primary key.
   * @param data - Updated record details.
   */
  async function updateDnsRecord(id: number | string, recordId: number, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/dns/${recordId}`, { method: 'PUT', body: JSON.stringify(data) })
  }

  /**
   * Deletes a DNS record.
   *
   * @param id - Domain primary key.
   * @param recordId - DNS record primary key.
   */
  async function deleteDnsRecord(id: number | string, recordId: number): Promise<void> {
    await request(`/domains/${id}/dns/${recordId}`, { method: 'DELETE' })
  }

  /**
   * Adds an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param source - Source alias.
   * @param destination - Destination email address.
   */
  async function addForwardingRule(id: number | string, source: string, destination: string): Promise<void> {
    await request(`/domains/${id}/email-forwarding`, { method: 'POST', body: JSON.stringify({ source, destination }) })
  }

  /**
   * Updates an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param ruleId - Forwarding rule primary key.
   * @param data - Updated rule details.
   */
  async function updateForwardingRule(id: number | string, ruleId: number, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/email-forwarding/${ruleId}`, { method: 'PUT', body: JSON.stringify(data) })
  }

  /**
   * Deletes an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param ruleId - Forwarding rule primary key.
   */
  async function deleteForwardingRule(id: number | string, ruleId: number): Promise<void> {
    await request(`/domains/${id}/email-forwarding/${ruleId}`, { method: 'DELETE' })
  }

  return {
    domains, clientDomains, current, page, pageSize, totalCount, loading, error,
    fetchAll, fetchByClient, fetchById, update, renew,
    setAutoRenew, setWhoisPrivacy, setLock, setDnsManagement, setEmailForwarding,
    modifyContact, getEppCode,
    addDnsRecord, updateDnsRecord, deleteDnsRecord,
    addForwardingRule, updateForwardingRule, deleteForwardingRule,
  }
})
