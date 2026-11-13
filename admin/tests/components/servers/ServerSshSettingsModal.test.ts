/**
 * Unit tests for the guided SSH setup dialog: it loads settings and setup, never shows a stored
 * private key, generates an identity and shows a hash-verifying installer command, shows the
 * network lines for the agent port, reads and pins a host key, explains a failed check in words and
 * flags an agent installed for another server, keeps an empty key field as "keep", confirms a save
 * beside its button while staying open, and never runs save and the health check together.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { flushPromises } from '@vue/test-utils'
import ServerSshSettingsModal from '@/components/servers/ServerSshSettingsModal.vue'
import { SshAgentProfile, SshProviderFailure } from '@/types/sshenums'
import { callOf, invalid, json, problem, stubFetch } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubDocsUrl } from '../../helpers/runtimeConfig'
import { serverSshSettingsDto, serverSshSetupDto } from '../../helpers/serverFixtures'

/** Settings as the API answers them. */
const SETTINGS = serverSshSettingsDto({ serverId: 4 })

/** Setup before an identity exists. */
const SETUP = serverSshSetupDto({ serverId: 4, bundleSha256: 'b'.repeat(64) })

/** Setup after an identity was generated. */
const SETUP_WITH_KEY = serverSshSetupDto({
  serverId: 4,
  bundleSha256: 'b'.repeat(64),
  publicKey: 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b hostpanel-server-4',
  binding: '22222222-2222-4222-8222-222222222222',
})

/**
 * Mounts the dialog for server 4 and waits for its two loads.
 *
 * @param docsUrl - Docs address the deployment injects through `config.js` (default empty).
 * @returns The mounted dialog.
 */
const mountDialog = async (docsUrl = ''): Promise<ReturnType<typeof mountWithApp>> => {
  stubDocsUrl(docsUrl)
  const wrapper = mountWithApp(ServerSshSettingsModal, { props: { serverId: 4, serverName: 'host1' } })
  await flushPromises()
  return wrapper
}

describe('ServerSshSettingsModal', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('draws the real steps with placeholder fields while the settings load', async () => {
    stubFetch(json(SETTINGS), json(SETUP))
    stubDocsUrl('')
    const wrapper = mountWithApp(ServerSshSettingsModal, { props: { serverId: 4, serverName: 'host1' } })
    // The load starts in onMounted, so the placeholder state is drawn on the next tick.
    await nextTick()

    expect(wrapper.get('[data-testid="server-ssh-body"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="server-ssh-step-connection"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(5)

    await flushPromises()

    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(true)
  })

  it('loads settings and setup, and leaves the private key field empty', async () => {
    const mock = stubFetch(json(SETTINGS), json(SETUP))
    const wrapper = await mountDialog()

    expect(callOf(mock, 0).url).toContain('/api/admin/servers/4/ssh-settings')
    expect(callOf(mock, 1).url).toContain('/api/admin/servers/4/ssh-setup')
    expect((wrapper.get('[data-testid="server-ssh-hostname"]').element as HTMLInputElement).value).toBe(SETTINGS.sshHostname)
    expect((wrapper.get('[data-testid="server-ssh-private-key"]').element as HTMLTextAreaElement).value).toBe('')
    expect(wrapper.find('[data-testid="server-ssh-install-command"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="server-ssh-generate"]').text()).toBe('Generate identity')
  })

  it('generates an identity and shows the hash-verifying installer command', async () => {
    const mock = stubFetch(json(SETTINGS), json(SETUP), json(SETUP_WITH_KEY), json(SETTINGS))
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-generate"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 2).method).toBe('POST')
    expect(callOf(mock, 2).url).toContain('/ssh-identity')
    const command = wrapper.get('[data-testid="server-ssh-install-command"]').text()
    expect(command).toContain('sha256sum -c -')
    expect(command).toContain('b'.repeat(64))
    expect(command).toContain(`--provider '${SshAgentProfile.Cwp}' --binding '${SETUP_WITH_KEY.binding}'`)
    expect(command).not.toContain('PRIVATE')
    expect(wrapper.get('[data-testid="server-ssh-generate"]').text()).toBe('Rotate identity')
  })

  it('shows the firewall and sshd lines that let only this HostPanel in on the agent port', async () => {
    stubFetch(json(SETTINGS), json(SETUP))
    const wrapper = await mountDialog()

    const network = wrapper.get('[data-testid="server-ssh-network"]').text()
    expect(network).toContain('CSF')
    expect(network).toContain(`csf -a 203.0.113.10 'HostPanel SSH key agent'`)
    expect(network).toContain(`ufw allow from 203.0.113.10 to any port ${SETTINGS.agentPort} proto tcp`)
    expect(network).toContain('PerSourcePenaltyExemptList 203.0.113.10')
  })

  it('asks for HostPanel\'s address rather than guessing one', async () => {
    stubFetch(json(SETTINGS), json({ ...SETUP, sourceAddresses: [] }))
    const wrapper = await mountDialog()

    expect(wrapper.find('[data-testid="server-ssh-network-lines"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="server-ssh-network-unset"]').text()).toContain('SshAgent__SourceAddresses')
  })

  it('reads the host key and pins it only through the form', async () => {
    const fingerprint = 'SHA256:' + 'Z'.repeat(43)
    const mock = stubFetch(json(SETTINGS), json(SETUP), json({ algorithm: 'ssh-ed25519', fingerprint, matchesPinned: false }), json(SETTINGS))
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-scan"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-testid="server-ssh-scanned"]').text()).toBe(`ssh-ed25519 ${fingerprint}`)
    expect(wrapper.get('[data-testid="server-ssh-scan-match"]').text()).toBe('Not the pinned key')
    await wrapper.get('[data-testid="server-ssh-use-scanned"]').trigger('click')
    // The badge follows the fingerprint field, not the answer the scan gave before it was used.
    expect(wrapper.get('[data-testid="server-ssh-scan-match"]').text()).toBe('Matches the pinned key')
    expect(wrapper.find('[data-testid="server-ssh-use-scanned"]').exists()).toBe(false)
    await wrapper.get('[data-testid="server-ssh-save"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 2).url).toContain('/ssh-settings/scan-host-key')
    expect((callOf(mock, 3).body as Record<string, unknown>).agentHostKeyFingerprint).toBe(fingerprint)
  })

  it('shows a failed host key read beside the button, not at the top of the dialog', async () => {
    stubFetch(json(SETTINGS), json(SETUP), problem('No answer from the agent host and port.', 503))
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-scan"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-ssh-scan-error"]').text()).toContain('No answer from the agent host and port.')
    expect(wrapper.find('[data-testid="server-ssh-error"]').exists()).toBe(false)
  })

  it('runs the health check and shows version and profile', async () => {
    stubFetch(
      json(SETTINGS),
      json(SETUP),
      json({ isHealthy: true, agentVersion: '1.1.0', error: null, provider: SshAgentProfile.Cwp }),
      json({ ...SETTINGS, isHealthy: true }),
    )
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-test"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-ssh-health"]').text()).toBe('Agent reachable')
    expect(wrapper.get('[data-testid="server-ssh-agent-version"]').text()).toBe('Agent 1.1.0 (cwp)')
  })

  it('shows the version alone when the agent sent no profile', async () => {
    stubFetch(json(SETTINGS), json(SETUP), json({ isHealthy: true, agentVersion: '1.0.0', error: null, provider: null }), json(SETTINGS))
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-test"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-ssh-agent-version"]').text()).toBe('Agent 1.0.0')
  })

  it('explains an agent installed for another server', async () => {
    stubFetch(
      json(SETTINGS),
      json(SETUP),
      json({ isHealthy: false, agentVersion: '1.1.0', error: SshProviderFailure.BindingMismatch, provider: SshAgentProfile.Cwp }),
      json({ ...SETTINGS, isHealthy: false }),
    )
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-test"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="server-ssh-binding-mismatch"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="server-ssh-agent-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="server-ssh-health"]').text()).toBe('Agent unreachable')
  })

  it('shows why a failed health check failed in words, not as a code', async () => {
    stubFetch(
      json(SETTINGS),
      json(SETUP),
      json({ isHealthy: false, agentVersion: null, error: SshProviderFailure.Unreachable, provider: null }),
      json({ ...SETTINGS, isHealthy: false }),
    )
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-test"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-ssh-agent-error"]').text()).toBe('Last check failed: the server could not be reached')
  })

  it('blocks save and the health check while either runs', async () => {
    stubFetch(json(SETTINGS), json(SETUP))
    const wrapper = await mountDialog()
    /** Settles the pending health check; replaced once the request starts. */
    let release: (answer: Response) => void = () => undefined
    const pending = vi.fn(() => new Promise<Response>((resolve) => {
      release = resolve
    }))
    vi.stubGlobal('fetch', pending)

    await wrapper.get('[data-testid="server-ssh-test"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-ssh-test"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="server-ssh-save"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="server-ssh-save"]').trigger('click')
    expect(pending).toHaveBeenCalledTimes(1)

    release(json({ isHealthy: true, agentVersion: '1.0.0', error: null, provider: null }))
    await flushPromises()
    expect(wrapper.get('[data-testid="server-ssh-save"]').attributes('disabled')).toBeUndefined()
  })

  it('sends an empty private key field as null, and shows a refused enable while staying open', async () => {
    const mock = stubFetch(json(SETTINGS), json(SETUP), invalid('Run a successful agent test with these settings before enabling SSH key management.', {}))
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-enabled"]').trigger('click')
    await wrapper.get('[data-testid="server-ssh-save"]').trigger('click')
    await flushPromises()

    const body = callOf(mock, 2).body as Record<string, unknown>
    expect(body.isEnabled).toBe(true)
    expect(body.agentPrivateKey).toBeNull()
    expect(wrapper.get('[data-testid="server-ssh-error"]').text()).toContain('successful agent test')
    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('confirms a save beside the button, stays open, and drops the confirmation once a field changes', async () => {
    stubFetch(json(SETTINGS), json(SETUP), json({ ...SETTINGS, sshPort: 2299 }))
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-ssh-saved"]').text()).toBe('Saved')
    expect(wrapper.emitted('close')).toBeUndefined()
    await wrapper.get('[data-testid="server-ssh-hostname"]').setValue('other.example.test')
    expect(wrapper.find('[data-testid="server-ssh-saved"]').exists()).toBe(false)
  })

  it('says beside the button that a refused save needs the highlighted fields fixed', async () => {
    stubFetch(json(SETTINGS), json(SETUP), invalid('One or more validation errors occurred.', { agentHost: ['Agent host must not be empty.'] }))
    const wrapper = await mountDialog()

    await wrapper.get('[data-testid="server-ssh-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-ssh-save-status"]').text()).toBe('Not saved: check the highlighted fields.')
    expect(wrapper.text()).toContain('Agent host must not be empty.')
    expect(wrapper.find('[data-testid="server-ssh-saved"]').exists()).toBe(false)
  })

  it('warns that an unsupported module has no installer and cannot be tested', async () => {
    stubFetch(json({ ...SETTINGS, isSupported: false, hasAgentPrivateKey: false }), json({ ...SETUP, profile: null }))
    const wrapper = await mountDialog()

    expect(wrapper.find('[data-testid="server-ssh-unsupported"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="server-ssh-generate"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="server-ssh-test"]').attributes('disabled')).toBeDefined()
  })

  it('hides the help link when docsUrl is empty', async () => {
    stubFetch(json(SETTINGS), json(SETUP))
    const wrapper = await mountDialog('')

    expect(wrapper.find('[data-testid="ssh-guide-link"]').exists()).toBe(false)
  })

  it('shows the help link with the correct href and rel when docsUrl is set', async () => {
    stubFetch(json(SETTINGS), json(SETUP))
    const wrapper = await mountDialog('https://docs.example.com')
    const link = wrapper.find('[data-testid="ssh-guide-link"]')

    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://docs.example.com/servers/ssh-key-agent')
    expect(link.attributes('rel')).toContain('noopener')
  })
})
