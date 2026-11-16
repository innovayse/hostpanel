/**
 * Endpoints behind the servers store: provisioning servers and server groups.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import type {
  ServerDto,
  ServerGroupDto,
  ServerGroupPayload,
  ServerListQuery,
  ServerModule,
  ServerOptionDto,
  ServerPayload,
  TestConnectionResultDto,
} from '@/types/server'
import type { ServerSshSettingsDto, ServerSshSettingsPayload, ServerSshSetupDto, SshAgentTestResultDto, SshHostKeyScanDto } from '@/types/serverssh'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/admin/servers` and `/admin/server-groups`.
 *
 * @returns One typed function per endpoint.
 */
export const useServersApi = () => {
  const { request } = useApi()

  /**
   * Lists one page of the provisioning servers.
   *
   * @param query - Page, page size, search text, module and last known state.
   * @returns The page and the total count.
   */
  const listServers = (query: ServerListQuery): Promise<PaginatedList<ServerDto>> =>
    request<PaginatedList<ServerDto>>(
      `${ApiPath.AdminServers}${buildQuery({
        page: query.page,
        pageSize: query.pageSize,
        search: query.search,
        module: query.module,
        state: query.state,
      })}`,
    )

  /**
   * Lists every provisioning server as a lean option, for the screens that choose a server or sum up all of them.
   *
   * @param module - Keep only the servers that run this module; every server when omitted.
   * @returns Every matching server, by name.
   */
  const listServerOptions = (module?: ServerModule): Promise<ServerOptionDto[]> =>
    request<ServerOptionDto[]>(`${ApiPath.AdminServerOptions}${buildQuery({ module })}`)

  /**
   * Creates a server.
   *
   * @param payload - Server data.
   * @returns Nothing the caller needs.
   */
  const createServer = (payload: ServerPayload): Promise<void> =>
    request<void>(ApiPath.AdminServers, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates a server.
   *
   * @param id - Server id.
   * @param payload - Updated server data.
   * @returns Nothing; the API answers 204.
   */
  const updateServer = (id: number, payload: ServerPayload): Promise<void> =>
    request<void>(`${ApiPath.AdminServers}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a server.
   *
   * @param id - Server id.
   * @returns Nothing; the API answers 204.
   */
  const deleteServer = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminServers}/${id}`, { method: HttpMethod.Delete })

  /**
   * Probes a server's connection.
   *
   * @param id - Server id.
   * @returns The probe result.
   */
  const testServerConnection = (id: number): Promise<TestConnectionResultDto> =>
    request<TestConnectionResultDto>(`${ApiPath.AdminServers}/${id}/test-connection`, { method: HttpMethod.Post })

  /**
   * Lists the server groups with their servers.
   *
   * @returns Every group.
   */
  const listServerGroups = (): Promise<ServerGroupDto[]> =>
    request<ServerGroupDto[]>(ApiPath.AdminServerGroups)

  /**
   * Creates a server group.
   *
   * @param payload - Group data.
   * @returns Nothing the caller needs.
   */
  const createServerGroup = (payload: ServerGroupPayload): Promise<void> =>
    request<void>(ApiPath.AdminServerGroups, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates a server group.
   *
   * @param id - Group id.
   * @param payload - Updated group data.
   * @returns Nothing; the API answers 204.
   */
  const updateServerGroup = (id: number, payload: ServerGroupPayload): Promise<void> =>
    request<void>(`${ApiPath.AdminServerGroups}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a server group.
   *
   * @param id - Group id.
   * @returns Nothing; the API answers 204.
   */
  const deleteServerGroup = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminServerGroups}/${id}`, { method: HttpMethod.Delete })

  /**
   * Reads a server's SSH key management settings. The private key is never part of the answer.
   *
   * @param id - Server id.
   * @returns The settings; disabled defaults when never configured.
   */
  const getServerSshSettings = (id: number): Promise<ServerSshSettingsDto> =>
    request<ServerSshSettingsDto>(`${ApiPath.AdminServers}/${id}/ssh-settings`)

  /**
   * Saves a server's SSH key management settings.
   *
   * @param id - Server id.
   * @param payload - The settings; a null private key keeps the stored one.
   * @returns The saved settings.
   */
  const saveServerSshSettings = (id: number, payload: ServerSshSettingsPayload): Promise<ServerSshSettingsDto> =>
    request<ServerSshSettingsDto>(`${ApiPath.AdminServers}/${id}/ssh-settings`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Pings a server's SSH key agent and records the outcome.
   *
   * @param id - Server id.
   * @returns Whether the agent answered, and its version or the reason it did not.
   */
  const testServerSshAgent = (id: number): Promise<SshAgentTestResultDto> =>
    request<SshAgentTestResultDto>(`${ApiPath.AdminServers}/${id}/ssh-settings/test`, { method: HttpMethod.Post })

  /**
   * Reads what is needed to install the SSH key agent on a server.
   *
   * @param id - Server id.
   * @returns Profile, public key, binding and bundle; never the private key.
   */
  const getServerSshSetup = (id: number): Promise<ServerSshSetupDto> =>
    request<ServerSshSetupDto>(`${ApiPath.AdminServers}/${id}/ssh-setup`)

  /**
   * Generates or rotates a server's ED25519 management identity.
   *
   * @param id - Server id.
   * @returns The setup with the new public key.
   */
  const generateServerSshIdentity = (id: number): Promise<ServerSshSetupDto> =>
    request<ServerSshSetupDto>(`${ApiPath.AdminServers}/${id}/ssh-identity`, { method: HttpMethod.Post })

  /**
   * Reads the host key the saved agent endpoint presents, for the administrator to verify.
   *
   * @param id - Server id.
   * @returns The presented key and whether it matches the pin.
   */
  const scanServerSshHostKey = (id: number): Promise<SshHostKeyScanDto> =>
    request<SshHostKeyScanDto>(`${ApiPath.AdminServers}/${id}/ssh-settings/scan-host-key`, { method: HttpMethod.Post })

  return {
    listServers,
    listServerOptions,
    createServer,
    updateServer,
    deleteServer,
    testServerConnection,
    listServerGroups,
    createServerGroup,
    updateServerGroup,
    deleteServerGroup,
    getServerSshSettings,
    saveServerSshSettings,
    testServerSshAgent,
    getServerSshSetup,
    generateServerSshIdentity,
    scanServerSshHostKey,
  }
}
