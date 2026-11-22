/**
 * Unit tests for PluginInstalledOverview: its five cards (details, installation, compatibility, permissions, release
 * notes) are read from the installed plugin alone, and a fact the server never recorded says so instead of vanishing.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginInstalledOverview from '@/components/plugins/details/PluginInstalledOverview.vue'
import { formatDateTime } from '@/lib/format'
import { PluginCapability, PluginHostAccess, PluginManifestRule, PluginSigner, PluginSource, PluginStatus } from '@/types/plugin'
import { createTestI18n } from '../../../helpers/i18n'
import { makeInstalled } from '../../../helpers/pluginDetails'

/** The facts of an upload that a server recorded: a package signed by a verified publisher, later replaced once. */
const recorded = {
  source: PluginSource.Registry,
  installedAt: '2026-03-04T10:30:00Z',
  updatedAt: '2026-05-06T08:15:00Z',
  installedBy: 'Ada Admin',
  signer: PluginSigner.VerifiedPublisher,
  signerKeyId: 'official-key',
  packageSizeBytes: 5 * 1024 * 1024,
  packageSha256: 'a'.repeat(64),
}

/**
 * Mounts the overview.
 *
 * @param plugin - Fields of the installed plugin to change.
 * @returns The wrapper.
 */
const mountOverview = (plugin: Parameters<typeof makeInstalled>[0] = {}) =>
  mount(PluginInstalledOverview, { props: { plugin: makeInstalled(plugin) }, global: { plugins: [createTestI18n()] } })

/**
 * Reads the text of one fact of the overview.
 *
 * @param wrapper - The mounted overview.
 * @param name - The fact's test id suffix after `plugin-details-overview-`.
 * @returns The fact's caption and value as text.
 */
const fact = (wrapper: ReturnType<typeof mountOverview>, name: string): string =>
  wrapper.get(`[data-testid="plugin-details-overview-${name}"]`).text()

describe('PluginInstalledOverview', () => {
  it('draws the five cards', () => {
    const wrapper = mountOverview()

    for (const card of ['details', 'installation', 'compatibility', 'permissions', 'notes']) {
      expect(wrapper.find(`[data-testid="ui-detail-card-${card}"]`).exists()).toBe(true)
    }
  })

  it('shows who made the plugin and what it is in the details card', () => {
    const wrapper = mountOverview({ author: 'Hostpanel', description: 'Card payments', type: 'payment' })
    const card = wrapper.get('[data-testid="ui-detail-card-details"]').text()

    expect(card).toContain('Hostpanel')
    expect(card).toContain('Card payments')
    expect(card).toContain('Payment gateway')
    expect(card).toContain('example-pay')
    expect(fact(wrapper, 'category')).toContain('Payments')
  })

  it('says when a plugin gives no description or author, and shows an unknown type as written', () => {
    const wrapper = mountOverview({ author: '', description: '', type: 'mystery' })
    const card = wrapper.get('[data-testid="ui-detail-card-details"]').text()

    expect(card).toContain('The plugin gives no description.')
    expect(card).toContain('Not given')
    expect(card).toContain('mystery')
  })

  it('shows the broken rule of a refused plugin in the details card', () => {
    const wrapper = mountOverview({ refusedRule: PluginManifestRule.CapabilityNotAllowed, refusedDetail: 'tickets' })

    expect(wrapper.get('[data-testid="plugin-details-refusal"]').text()).toContain('tickets')
  })

  it('shows what the server recorded of the install, with the dates in the shared date-time format', () => {
    const wrapper = mountOverview(recorded)

    expect(fact(wrapper, 'source')).toContain('Plugin registry')
    expect(fact(wrapper, 'installed-at')).toContain(formatDateTime(recorded.installedAt))
    expect(fact(wrapper, 'updated-at')).toContain(formatDateTime(recorded.updatedAt))
    expect(fact(wrapper, 'installed-by')).toContain('Ada Admin')
    expect(fact(wrapper, 'signer')).toContain('A verified publisher')
    expect(fact(wrapper, 'signer-key')).toContain('official-key')
    expect(fact(wrapper, 'size')).toContain('5 MB')
    expect(fact(wrapper, 'sha256')).toContain(recorded.packageSha256)
  })

  it('says "not recorded" for every install fact of a plugin installed before installs were recorded', () => {
    const wrapper = mountOverview()

    for (const name of ['source', 'installed-at', 'installed-by', 'signer', 'size', 'sha256']) {
      expect(fact(wrapper, name)).toContain('Not recorded')
    }
    expect(fact(wrapper, 'updated-at')).toContain('Not updated since it was installed')
    expect(wrapper.find('[data-testid="plugin-details-overview-signer-key"]').exists()).toBe(false)
  })

  it('names the state of a loaded, a waiting and a refused plugin', () => {
    expect(fact(mountOverview({ isLoaded: true }), 'state')).toContain('Loaded and running')
    expect(fact(mountOverview({ isLoaded: false }), 'state')).toContain('Loads after the next restart')
    expect(fact(mountOverview({ status: PluginStatus.Refused, isLoaded: false }), 'state')).toContain('Refused at startup')
    expect(fact(mountOverview({ isLoaded: false, refusedRule: PluginManifestRule.AssemblyLoadFailed, refusedDetail: 'Could not load file or assembly' }), 'state'))
      .toContain('Failed to load at startup')
  })

  it('shows the load error of a plugin whose assembly did not load', () => {
    const wrapper = mountOverview({ isLoaded: false, refusedRule: PluginManifestRule.AssemblyLoadFailed, refusedDetail: 'Could not load file or assembly' })

    expect(wrapper.get('[data-testid="plugin-details-refusal"]').text()).toContain('Could not load file or assembly')
  })

  it('shows the plugin SDK version the package was built on next to the host versions, or says none was given', () => {
    expect(fact(mountOverview({ minHostVersion: '1.4.0', sdkPackageVersion: '3.11.0' }), 'sdk')).toContain('3.11.0')
    expect(fact(mountOverview({}), 'sdk')).toContain('Not given')
  })

  it('shows the installed version and the staged one only while an upgrade waits', () => {
    const plain = mountOverview()
    const staged = mountOverview({ stagedVersion: '3.0.0' })

    expect(fact(plain, 'version')).toContain('2.0.0')
    expect(plain.find('[data-testid="plugin-details-overview-staged"]').exists()).toBe(false)
    expect(fact(staged, 'staged')).toContain('Version 3.0.0 staged')
  })

  it.each([
    [{ minHostVersion: '1.0.0', maxHostVersion: '2.0.0' }, '1.0.0 to 2.0.0'],
    [{ minHostVersion: '1.0.0', maxHostVersion: null }, '1.0.0 or newer'],
    [{ minHostVersion: null, maxHostVersion: '2.0.0' }, '2.0.0 or older'],
    [{ minHostVersion: null, maxHostVersion: null }, 'any host version'],
  ])('words the host range %j', (range, expected) => {
    const wrapper = mountOverview(range)

    expect(fact(wrapper, 'hosts')).toContain(expected)
  })

  it('lists the capabilities and host access the manifest asks for, and says none when it asks for nothing', () => {
    const none = mountOverview()
    const some = mountOverview({ capabilities: [PluginCapability.Endpoints], hostAccess: [PluginHostAccess.Ssh] })

    expect(none.find('[data-testid="plugin-details-capabilities"]').exists()).toBe(false)
    expect(none.get('[data-testid="plugin-details-permissions-none"]').text()).toContain('No access to client hosting')
    expect(some.find('[data-testid="plugin-details-permissions-none"]').exists()).toBe(false)
    expect(some.get(`[data-testid="plugin-details-capability-${PluginCapability.Endpoints}"]`).text()).toContain('pages, menus and widgets')
    expect(some.get(`[data-testid="plugin-details-host-access-${PluginHostAccess.Ssh}"]`).text()).toContain('over SSH')
  })

  it('draws the release notes as Markdown with links left as text, or says there are none', () => {
    const noted = mountOverview({ changelog: '- Faster **refunds**, see [notes](https://example.com/notes)' })
    const bare = mountOverview()
    const notes = noted.get('[data-testid="plugin-details-changelog"]')

    expect(notes.find('strong').text()).toBe('refunds')
    expect(notes.find('a').exists()).toBe(false)
    expect(notes.text()).toContain('notes')
    expect(bare.get('[data-testid="plugin-details-changelog"]').text()).toBe('No release notes.')
  })
})
