import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { ServerDto, ServerGroupDto, ServerPayload, ServerGroupPayload, TestConnectionResultDto } from '../types/server.types'

/**
 * Manages all provisioning server and server group state for the admin panel.
 *
 * Servers and groups are loaded independently. CRUD actions optimistically refresh state after each mutation.
 */
export const useServersStore = defineStore('servers', () => {
  const { request } = useApi()

  /** All provisioning servers. */
  const servers = ref<ServerDto[]>([])

  /** All server groups with their assigned servers. */
  const groups = ref<ServerGroupDto[]>([])

  /** True while an async request is in flight. */
  const loading = ref(false)

  /** Last error message from a failed request, or null. */
  const error = ref<string | null>(null)

  /**
   * Loads all servers from the API.
   *
   * @returns Promise resolving when servers are loaded.
   */
  async function fetchServers(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      servers.value = await request<ServerDto[]>('/admin/servers')
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load servers'
    } finally {
      loading.value = false
    }
  }

  /**
   * Loads all server groups from the API.
   *
   * @returns Promise resolving when groups are loaded.
   */
  async function fetchGroups(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      groups.value = await request<ServerGroupDto[]>('/admin/server-groups')
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load server groups'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new server and refreshes the servers list.
   *
   * @param payload - Server creation data.
   * @returns Promise resolving when the server is created.
   */
  async function createServer(payload: ServerPayload): Promise<void> {
    await request('/admin/servers', { method: 'POST', body: JSON.stringify(payload) })
    await fetchServers()
  }

  /**
   * Updates an existing server and refreshes the servers list.
   *
   * @param id - Server identifier.
   * @param payload - Updated server data.
   * @returns Promise resolving when the update completes.
   */
  async function updateServer(id: number, payload: ServerPayload): Promise<void> {
    await request(`/admin/servers/${id}`, { method: 'PUT', body: JSON.stringify(payload) })
    await fetchServers()
  }

  /**
   * Deletes a server and refreshes the servers list.
   *
   * @param id - Server identifier.
   * @returns Promise resolving when the deletion completes.
   */
  async function deleteServer(id: number): Promise<void> {
    await request(`/admin/servers/${id}`, { method: 'DELETE' })
    await fetchServers()
  }

  /**
   * Tests the connection to a server and updates its status in the list.
   *
   * @param id - Server identifier.
   * @returns The test result DTO.
   */
  async function testConnection(id: number): Promise<TestConnectionResultDto> {
    const result = await request<TestConnectionResultDto>(`/admin/servers/${id}/test-connection`, { method: 'POST' })
    await fetchServers()
    return result
  }

  /**
   * Tests the connection to a server, persists the result, and refreshes the list.
   *
   * @param id - Server identifier.
   * @returns The test result DTO.
   */
  async function testConnection(id: number): Promise<TestConnectionResultDto> {
    const result = await request<TestConnectionResultDto>(`/admin/servers/${id}/test-connection`, { method: 'POST' })
    await fetchServers()
    return result
  }

  /**
   * Creates a new server group and refreshes both servers and groups.
   *
   * @param payload - Group creation data.
   * @returns Promise resolving when the group is created.
   */
  async function createGroup(payload: ServerGroupPayload): Promise<void> {
    await request('/admin/server-groups', { method: 'POST', body: JSON.stringify(payload) })
    await Promise.all([fetchServers(), fetchGroups()])
  }

  /**
   * Updates an existing server group and refreshes both servers and groups.
   *
   * @param id - Group identifier.
   * @param payload - Updated group data.
   * @returns Promise resolving when the update completes.
   */
  async function updateGroup(id: number, payload: ServerGroupPayload): Promise<void> {
    await request(`/admin/server-groups/${id}`, { method: 'PUT', body: JSON.stringify(payload) })
    await Promise.all([fetchServers(), fetchGroups()])
  }

  /**
   * Deletes a server group and refreshes both servers and groups.
   *
   * @param id - Group identifier.
   * @returns Promise resolving when the deletion completes.
   */
  async function deleteGroup(id: number): Promise<void> {
    await request(`/admin/server-groups/${id}`, { method: 'DELETE' })
    await Promise.all([fetchServers(), fetchGroups()])
  }

  return {
    servers,
    groups,
    loading,
    error,
    fetchServers,
    fetchGroups,
    createServer,
    updateServer,
    deleteServer,
    testConnection,
    createGroup,
    updateGroup,
    deleteGroup,
  }
})
