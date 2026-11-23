/**
 * Fixtures for the servers section of a panel plugin Settings tab: what `GET /api/admin/plugins/{id}/servers` answers for a
 * plugin that serves one module with a tested server and a never tested one, and for a plugin that serves no server module.
 */
import type { PluginServersDto } from '../../src/types/plugin'
import type { ServerOptionDto } from '../../src/types/server'

/** A server on the module of the plugin whose last connection test succeeded. */
export const testedServer: ServerOptionDto = {
  id: 11,
  name: 'Frankfurt 1',
  hostname: 'fra1.example.test',
  ipAddress: '203.0.113.11',
  isOnline: true,
  lastTestedAt: '2026-01-02T10:00:00Z',
  accountsCount: 4,
  limits: { maxAccounts: 100, isDefault: false, isDisabled: false },
}

/** A server on the module of the plugin that was never tested. */
export const untestedServer: ServerOptionDto = {
  id: 12,
  name: 'Helsinki 1',
  hostname: 'hel1.example.test',
  ipAddress: null,
  isOnline: null,
  lastTestedAt: null,
  accountsCount: null,
  limits: { maxAccounts: null, isDefault: false, isDisabled: false },
}

/** The answer for a panel plugin that serves one module and has two servers on it. */
export const panelServers: PluginServersDto = {
  modules: ['example-panel'],
  servers: [testedServer, untestedServer],
}

/** The answer for a panel plugin that serves one module no server uses yet. */
export const panelWithoutServers: PluginServersDto = {
  modules: ['example-panel'],
  servers: [],
}

/** The answer for a plugin that serves no server module. */
export const notAPanel: PluginServersDto = {
  modules: [],
  servers: [],
}
