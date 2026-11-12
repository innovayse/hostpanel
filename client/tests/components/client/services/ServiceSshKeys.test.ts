/**
 * Unit tests for ServiceSshKeys: each access state, the list, the add form's local checks and
 * the revoke confirmation.
 *
 * The real SSH key store, API composable and transport run against a stub server playing the BFF;
 * the assertions are on the requests that reached it.
 *
 * @module tests/components/client/services/ServiceSshKeys.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServiceSshKeys from '~/components/client/services/ServiceSshKeys.vue'
import { useSshKeysStore } from '~/stores/sshkeys'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { HttpMethod } from '~/types/httpmethod'
import { SshAccessState } from '~/types/sshaccessstate'
import { SshKeySyncState } from '~/types/sshkeysyncstate'

/** The BFF path of the SSH access of service 6. */
const ACCESS_PATH = '/api/portal/client/services/6/ssh'

/** The BFF path of the SSH keys of service 6. */
const KEYS_PATH = '/api/portal/client/services/6/ssh/keys'

/** The BFF path of the key `k1` of service 6. */
const KEY_PATH = `${KEYS_PATH}/k1`

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** A real ED25519 public key line. */
const KEY = 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b me@host'

/**
 * Answers the SSH reads of service 6.
 *
 * @param state - The access state the API answers.
 * @param keys - The keys the API lists.
 */
const answerReads = (state: SshAccessState, keys: Record<string, unknown>[] = []): void => {
  table[`GET ${ACCESS_PATH}`] = {
    body: {
      state,
      hostname: 'cwp.example.test',
      port: 2222,
      username: 'web',
      connectCommand: state === SshAccessState.Available ? 'ssh -p 2222 web@cwp.example.test' : null,
      maxKeys: 20
    }
  }
  table[`GET ${KEYS_PATH}`] = { body: keys }
}

/**
 * The requests that tried to add a key.
 *
 * @returns The requests, oldest first.
 */
const additions = () => requestsTo(stub.requests, KEYS_PATH).filter((request) => request.method === HttpMethod.Post)

/**
 * Mounts the panel for service 6 and waits until its first read has been handled.
 *
 * @returns The mounted panel, once loaded.
 */
const mountLoaded = async (): Promise<ReturnType<typeof mountClient>> => {
  const wrapper = mountClient(ServiceSshKeys, { props: { serviceId: '6' } })
  await vi.waitFor(() => expect(requestsTo(stub.requests, ACCESS_PATH)).toHaveLength(1))
  await vi.waitFor(() => expect(useSshKeysStore().loading).toBe(false))
  await wrapper.vm.$nextTick()
  return wrapper
}

/**
 * Mounts the panel for service 6 in the given access state with the given keys.
 *
 * @param state - The access state the API answers.
 * @param keys - The keys the API lists.
 * @returns The mounted panel, once loaded.
 */
const mountIn = async (state: SshAccessState, keys: Record<string, unknown>[] = []): Promise<ReturnType<typeof mountClient>> => {
  answerReads(state, keys)
  return mountLoaded()
}

/** One installed key. */
const KEY_ROW = { keyId: 'k1', label: 'laptop', fingerprint: 'SHA256:abc', createdAt: '2026-10-27T00:00:00Z', syncState: SshKeySyncState.Installed, isStale: false, presentOnServer: true }

describe('ServiceSshKeys', () => {
  beforeEach(async () => {
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('loads this service and shows the connect command and the empty state', async () => {
    const wrapper = await mountIn(SshAccessState.Available)

    expect(requestsTo(stub.requests, ACCESS_PATH)).toHaveLength(1)
    expect(requestsTo(stub.requests, KEYS_PATH)).toHaveLength(1)
    expect(wrapper.find('[data-testid="service-ssh-command"]').text()).toBe('ssh -p 2222 web@cwp.example.test')
    expect(wrapper.find('[data-testid="service-ssh-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-ssh-form"]').exists()).toBe(true)
  })

  it('offers a deployment guide filled in with this account', async () => {
    const wrapper = await mountIn(SshAccessState.Available)

    expect(wrapper.find('[data-testid="service-ssh-howto"]').text()).toContain('client.services.ssh.deploy.title')
    await wrapper.find('[data-testid="ui-accordion-toggle-1"]').trigger('click')
    const html = wrapper.find('[data-testid="service-ssh-deploy"]').html()
    expect(html).toContain('ssh -p 2222 -i ~/.ssh/deploy_key')
    expect(html).toContain('web@cwp.example.test:public_html/')
  })

  it('offers no deployment guide without a form', async () => {
    const wrapper = await mountIn(SshAccessState.NotConfigured)

    expect(wrapper.find('[data-testid="service-ssh-howto"]').exists()).toBe(false)
  })

  it('lets keys be revoked when SSH was switched off on the server, but offers no form', async () => {
    const wrapper = await mountIn(SshAccessState.NotConfigured, [KEY_ROW])

    expect(wrapper.find('[data-testid="service-ssh-revoke"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-ssh-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-ssh-connection"]').exists()).toBe(false)
  })

  it('flags a key still off the server just after the service came back', async () => {
    const wrapper = await mountIn(SshAccessState.Available, [{ ...KEY_ROW, syncState: SshKeySyncState.Suspended, presentOnServer: false }])

    expect(wrapper.find('[data-testid="service-ssh-key-badge"]').text()).toBe('client.services.ssh.sync.Suspended')
    expect(wrapper.find('[data-testid="service-ssh-revoke"]').exists()).toBe(true)
  })

  it('offers no revoke for an inactive service', async () => {
    const wrapper = await mountIn(SshAccessState.ServiceInactive)

    expect(wrapper.find('[data-testid="service-ssh-revoke"]').exists()).toBe(false)
  })

  it.each([SshAccessState.ShellDisabled, SshAccessState.Unsupported, SshAccessState.NotConfigured, SshAccessState.Unreachable])(
    'explains the %s state and offers no form', async (state) => {
      const wrapper = await mountIn(state)

      expect(wrapper.find('[data-testid="service-ssh-state"]').text()).toContain(`client.services.ssh.state.${state}`)
      expect(wrapper.find('[data-testid="service-ssh-form"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="service-ssh-command"]').exists()).toBe(false)
      // Nothing that suggests a login or a form the customer cannot use.
      expect(wrapper.find('[data-testid="service-ssh-connection"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="service-ssh-empty"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="service-ssh-keys"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="service-ssh-howto"]').exists()).toBe(false)
    })

  it('shows a load failure with a retry', async () => {
    table[`GET ${ACCESS_PATH}`] = { status: 404, body: { detail: 'No such client service.' } }
    const wrapper = mountClient(ServiceSshKeys, { props: { serviceId: '6' } })

    await vi.waitFor(() => expect(wrapper.find('[data-testid="service-ssh-load-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="service-ssh-load-error"]').text()).toContain('No such client service.')
    expect(wrapper.find('[data-testid="service-ssh-retry"]').exists()).toBe(true)
  })

  it('lists keys with fingerprint and flags a stale one, which can still be revoked', async () => {
    const wrapper = await mountIn(SshAccessState.Available, [KEY_ROW, { ...KEY_ROW, keyId: 'k2', label: 'old', isStale: true }])

    const rows = wrapper.findAll('[data-testid="service-ssh-key"]')
    expect(rows).toHaveLength(2)
    expect(rows[0]?.find('[data-testid="service-ssh-key-fingerprint"]').text()).toBe('SHA256:abc')
    expect(rows[0]?.find('[data-testid="service-ssh-revoke"]').exists()).toBe(true)
    expect(rows[1]?.find('[data-testid="service-ssh-key-badge"]').text()).toContain('client.services.ssh.sync.Stale')
    expect(rows[1]?.find('[data-testid="service-ssh-revoke"]').exists()).toBe(true)
  })

  it('refuses a private key in the browser', async () => {
    const wrapper = await mountIn(SshAccessState.Available)

    await wrapper.find('[data-testid="service-ssh-public-key"]').setValue('-----BEGIN OPENSSH PRIVATE KEY-----\nabc')
    await wrapper.find('[data-testid="service-ssh-label"]').setValue('x')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('client.services.ssh.localError.privateKey')
    expect(additions()).toHaveLength(0)
  })

  it('pre-fills the label from the key comment and adds the key', async () => {
    table[`POST ${KEYS_PATH}`] = { body: { ...KEY_ROW, keyId: 'new', label: 'me@host' } }
    const wrapper = await mountIn(SshAccessState.Available)

    await wrapper.find('[data-testid="service-ssh-public-key"]').setValue(KEY)
    expect((wrapper.find('[data-testid="service-ssh-label"]').element as HTMLInputElement).value).toBe('me@host')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.findAll('[data-testid="service-ssh-key"]')).toHaveLength(1))
    expect(additions()).toHaveLength(1)
    expect(bodyOf(additions()[0])).toEqual({ label: 'me@host', publicKey: KEY })
  })

  it('shows the API refusal above the form', async () => {
    table[`POST ${KEYS_PATH}`] = { status: 409, body: { detail: 'This key is already installed on the service.' } }
    const wrapper = await mountIn(SshAccessState.Available)

    await wrapper.find('[data-testid="service-ssh-public-key"]').setValue(KEY)
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('already installed'))
  })

  it('revokes only after confirmation', async () => {
    table[`DELETE ${KEY_PATH}`] = { status: 204 }
    const wrapper = await mountIn(SshAccessState.Available, [KEY_ROW])

    await wrapper.find('[data-testid="service-ssh-revoke"]').trigger('click')
    expect(requestsTo(stub.requests, KEY_PATH)).toHaveLength(0)
    expect(wrapper.find('[data-testid="ui-confirm-dialog"]').text()).toContain('client.services.ssh.revokeConfirmTitle')
    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await vi.waitFor(() => expect(requestsTo(stub.requests, KEY_PATH)).toHaveLength(1))
    expect(requestsTo(stub.requests, KEY_PATH)[0]?.method).toBe(HttpMethod.Delete)
    await vi.waitFor(() => expect(wrapper.findAll('[data-testid="service-ssh-key"]')).toHaveLength(0))
  })
})
