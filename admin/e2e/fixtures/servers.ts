/**
 * Fixtures for the servers screen: one provisioning server as a page of `GET /api/admin/servers` returns it,
 * with its SSH key management settings and agent installer setup.
 */
import type { PaginatedList } from '../../src/types/paginatedlist'
import type { ServerDto, ServerModuleOptionDto, TestConnectionResultDto } from '../../src/types/server'
import { ServerModuleSource } from '../../src/types/serverenums'
import type { ServerSshSettingsDto, ServerSshSetupDto } from '../../src/types/serverssh'

/** The control panel modules the server form and the module filter offer: the host's own and one a plugin declares. */
export const serverModules: ServerModuleOptionDto[] = [
  { id: 'CPanel', name: 'CPanel', source: ServerModuleSource.BuiltIn },
  { id: 'hestiacp', name: 'Hestia Control Panel', source: ServerModuleSource.Plugin },
]

/** A reachable control-panel server that is the default for its module. */
export const hostServer: ServerDto = {
  id: 1,
  name: 'host1',
  endpoint: {
    hostname: 'host1.example.test',
    apiHost: null,
    ipAddress: '203.0.113.10',
    assignedIpAddresses: null,
    useSSL: true,
    acceptsSelfSignedCertificate: false,
  },
  module: 'CPanel',
  username: 'root',
  limits: { maxAccounts: 100, isDefault: true, isDisabled: false },
  hosting: { datacenter: null, serverStatusAddress: null, monthlyCost: 20 },
  nameServers: {
    ns1Hostname: null,
    ns1Ip: null,
    ns2Hostname: null,
    ns2Ip: null,
    ns3Hostname: null,
    ns3Ip: null,
    ns4Hostname: null,
    ns4Ip: null,
    ns5Hostname: null,
    ns5Ip: null,
  },
  serverGroupId: null,
  serverGroupName: null,
  createdAt: '2026-01-01T00:00:00Z',
  isOnline: null,
  lastTestedAt: null,
  accountsCount: 12,
  credentials: { hasPassword: false, hasApiToken: false, hasAccessHash: true },
  usage: { activeServices: 3, monthlyRevenue: 45, currency: 'USD' },
}

/**
 * Wraps servers in the page shape `GET /api/admin/servers` answers with.
 *
 * @param items - The rows of the page.
 * @returns The page.
 */
export const serverPage = (items: ServerDto[]): PaginatedList<ServerDto> => ({
  items,
  totalCount: items.length,
  pageNumber: 1,
  pageSize: 20,
  totalPages: 1,
})

/** `POST /api/admin/servers/1/test-connection` when the panel answers. */
export const connectionOk: TestConnectionResultDto = {
  connected: true,
  accountsCount: 12,
  version: '7.4.2',
  errorMessage: null,
  testedAt: '2026-10-01T00:00:00Z',
}

/** SSH key management settings of {@link hostServer}, with a management key already stored. */
export const hostServerSsh: ServerSshSettingsDto = {
  serverId: hostServer.id,
  isSupported: true,
  isEnabled: false,
  sshHostname: 'host1.example.test',
  sshPort: 2222,
  agentHost: 'host1.internal.test',
  agentPort: 22,
  agentUsername: 'hpsshkeys',
  hasAgentPrivateKey: true,
  agentHostKeyFingerprint: 'SHA256:p6R0QgF93KJCG8T5VLbbvfWcFGdwzv2ViKOejXV5TLw',
  connectTimeoutSeconds: 10,
  operationTimeoutSeconds: 20,
  isHealthy: null,
  lastHealthCheckAt: null,
}

/** The agent profile id the e2e servers install: the id a panel plugin ships. */
export const SSH_AGENT_PROFILE = 'examplepanel'

/** Agent installer setup of {@link hostServer} before any identity exists, with no source address configured. */
export const hostServerSshSetup: ServerSshSetupDto = {
  serverId: hostServer.id,
  profile: SSH_AGENT_PROFILE,
  publicKey: null,
  binding: null,
  agentUsername: 'hpsshkeys',
  bundleVersion: '1.1.0',
  bundleSha256: 'e'.repeat(64),
  bundlePath: '/api/public/ssh-agent/bundle.tar.gz',
  sourceAddresses: [],
}
