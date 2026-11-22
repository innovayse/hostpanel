/**
 * Fixtures of the servers and integrations tests: a provisioning server, a group, and a server's
 * SSH key management settings and installer setup, as the admin API returns them.
 */
import type { ServerDto, ServerGroupDto, ServerModuleOptionDto, ServerOptionDto } from '@/types/server'
import { ServerModuleSource } from '@/types/serverenums'
import type { ServerSshSettingsDto, ServerSshSetupDto } from '@/types/serverssh'
import { SshAgentProfile } from '@/types/sshenums'

/**
 * Builds a provisioning server.
 *
 * @param overrides - Fields to change on the default server.
 * @returns The server.
 */
export const serverDto = (overrides: Partial<ServerDto> = {}): ServerDto => ({
  id: 1,
  name: 'host1',
  endpoint: { hostname: 'host1.example.com', ipAddress: '203.0.113.10', assignedIpAddresses: null, useSSL: true, acceptsSelfSignedCertificate: false },
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
  isOnline: true,
  lastTestedAt: null,
  accountsCount: 12,
  credentials: { hasPassword: true, hasApiToken: false, hasAccessHash: true },
  usage: { activeServices: 3, monthlyRevenue: 45, currency: 'USD' },
  ...overrides,
})

/**
 * Builds the control panel modules the module list endpoint answers: the host's own ones, and one a plugin declares.
 *
 * @param overrides - Modules to add after the defaults.
 * @returns The modules.
 */
export const serverModuleOptionDtos = (overrides: ServerModuleOptionDto[] = []): ServerModuleOptionDto[] => [
  { id: 'CPanel', name: 'CPanel', source: ServerModuleSource.BuiltIn },
  { id: 'hestiacp', name: 'Hestia Control Panel', source: ServerModuleSource.Plugin },
  ...overrides,
]

/**
 * Builds a server as the unpaged option lookup returns it.
 *
 * @param overrides - Fields to change on the default option.
 * @returns The option.
 */
export const serverOptionDto = (overrides: Partial<ServerOptionDto> = {}): ServerOptionDto => ({
  id: 1,
  name: 'host1',
  hostname: 'host1.example.com',
  ipAddress: '203.0.113.10',
  isOnline: true,
  accountsCount: 12,
  limits: { maxAccounts: 100, isDefault: true, isDisabled: false },
  ...overrides,
})

/**
 * Builds a server group.
 *
 * @param overrides - Fields to change on the default group.
 * @returns The group.
 */
export const serverGroupDto = (overrides: Partial<ServerGroupDto> = {}): ServerGroupDto => ({
  id: 3,
  name: 'Shared',
  fillType: 'LeastFull',
  servers: [serverDto()],
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

/**
 * Builds a server's SSH key management settings: an agent configured with a stored key, not enabled.
 *
 * @param overrides - Fields to change on the default settings.
 * @returns The settings.
 */
export const serverSshSettingsDto = (overrides: Partial<ServerSshSettingsDto> = {}): ServerSshSettingsDto => ({
  serverId: 1,
  isSupported: true,
  isEnabled: false,
  sshHostname: 'host1.example.com',
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
  ...overrides,
})

/**
 * Builds a server's SSH agent installer setup before an identity exists.
 *
 * @param overrides - Fields to change on the default setup.
 * @returns The setup.
 */
export const serverSshSetupDto = (overrides: Partial<ServerSshSetupDto> = {}): ServerSshSetupDto => ({
  serverId: 1,
  profile: SshAgentProfile.Cwp,
  publicKey: null,
  binding: null,
  agentUsername: 'hpsshkeys',
  bundleVersion: '1.1.0',
  bundleSha256: 'd'.repeat(64),
  bundlePath: '/api/public/ssh-agent/bundle.tar.gz',
  sourceAddresses: ['203.0.113.10'],
  ...overrides,
})
