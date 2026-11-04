/**
 * Tests for `server/utils/pluginBackendPaths.ts`: the backend's plugin endpoints of the client panel,
 * the host's own under the reserved `ui` segment and a plugin's routes below its id.
 *
 * @module tests/server/utils/pluginBackendPaths.test
 */

import { describe, expect, it } from 'vitest'
import { PluginBackendPaths } from '~/server/utils/pluginBackendPaths'

describe('PluginBackendPaths', () => {
  it('reads the index and the page lookup by address under the reserved ui segment, the address as one query value', () => {
    expect(PluginBackendPaths.ui).toBe('/client/plugins/ui')
    expect(PluginBackendPaths.pageByPath('acme', '/sites/a%20b')).toBe('/client/plugins/ui/acme/pages/by-path?path=%2Fsites%2Fa%2520b')
    expect(PluginBackendPaths.pageByPath('acme', '/')).toBe('/client/plugins/ui/acme/pages/by-path?path=%2F')
  })

  it('calls a plugin route below the plugin id, so a plugin route named pages is not the host page', () => {
    expect(PluginBackendPaths.route('acme', 'pages/drafts')).toBe('/client/plugins/acme/pages/drafts')
    expect(PluginBackendPaths.route('acme', 'sites/7?page=2')).toBe('/client/plugins/acme/sites/7?page=2')
  })
})
