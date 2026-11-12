/**
 * Unit tests for the SSH key store, through the real API composable and transport against a stub
 * server: what loads when, what a refusal leaves behind, and that a private key never leaves the
 * browser.
 *
 * @module tests/stores/sshkeys.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import { requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubReply, StubServer } from '../helpers/stubServer'
import { useSshKeysStore } from '~/stores/sshkeys'
import { ActionOutcome } from '~/types/actionoutcome'
import { SshAccessState } from '~/types/sshaccessstate'
import { SshKeyLocalError } from '~/types/sshkeylocalerror'
import { SshKeySyncState } from '~/types/sshkeysyncstate'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test, by `METHOD /path`. */
let table: StubRouteTable

/** A real ED25519 public key line. */
const KEY = 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b me@host'

/**
 * The BFF path of a service's SSH access.
 *
 * @param serviceId - The service id.
 * @returns The access path.
 */
const accessPath = (serviceId: string): string => `/api/portal/client/services/${serviceId}/ssh`

/**
 * The BFF path of a service's SSH keys.
 *
 * @param serviceId - The service id.
 * @returns The key list path.
 */
const keysPath = (serviceId: string): string => `${accessPath(serviceId)}/keys`

/**
 * An access summary in the given state.
 *
 * @param state - The access state.
 * @returns The summary.
 */
const access = (state: SshAccessState): Record<string, unknown> =>
  ({ state, hostname: 'h', port: 22, username: 'web', connectCommand: null, maxKeys: 2 })

/**
 * A listed key.
 *
 * @param keyId - Its id.
 * @returns The key.
 */
const key = (keyId: string): Record<string, unknown> =>
  ({ keyId, label: 'l', fingerprint: 'SHA256:x', createdAt: '', syncState: SshKeySyncState.Installed, isStale: false, presentOnServer: true })

/**
 * A refusal as the BFF answers it.
 *
 * @param detail - The API's sentence.
 * @returns The stub reply.
 */
const refusal = (detail: string): StubReply => ({ status: 400, body: { detail } })

describe('useSshKeysStore', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('loads access and keys for exactly the given service', async () => {
    table[`GET ${accessPath('42')}`] = { body: access(SshAccessState.Available) }
    table[`GET ${keysPath('42')}`] = { body: [key('a')] }
    const store = useSshKeysStore()

    const pending = store.load('42')
    expect(store.loading).toBe(true)
    await pending

    expect(requestsTo(stub.requests, accessPath('42'))).toHaveLength(1)
    expect(requestsTo(stub.requests, keysPath('42'))).toHaveLength(1)
    expect(stub.requests).toHaveLength(2)
    expect(store.loading).toBe(false)
    expect(store.canManage).toBe(true)
    expect(store.keys).toHaveLength(1)
  })

  it('skips the key list for an inactive service', async () => {
    table[`GET ${accessPath('42')}`] = { body: access(SshAccessState.ServiceInactive) }
    const store = useSshKeysStore()
    await store.load('42')
    expect(requestsTo(stub.requests, keysPath('42'))).toHaveLength(0)
    expect(store.canManage).toBe(false)
    expect(store.canRevoke).toBe(false)
  })

  it('cannot manage in any state but available', async () => {
    for (const state of [SshAccessState.Unsupported, SshAccessState.ShellDisabled, SshAccessState.NotConfigured, SshAccessState.Unreachable]) {
      table[`GET ${accessPath('1')}`] = { body: access(state) }
      table[`GET ${keysPath('1')}`] = { body: [] }
      const store = useSshKeysStore()
      await store.load('1')
      expect(store.canManage).toBe(false)
      expect(store.canRevoke).toBe(true)
    }
  })

  it('keeps the API sentence when loading fails', async () => {
    table[`GET ${accessPath('9')}`] = { status: 404, body: { detail: 'No such client service.' } }
    const store = useSshKeysStore()
    await store.load('9')
    expect(store.loadError).toBe('No such client service.')
    expect(store.loading).toBe(false)
  })

  it('never sends a private key', async () => {
    const store = useSshKeysStore()
    expect(await store.add('1', 'x', '-----BEGIN OPENSSH PRIVATE KEY-----\nabc')).toBe(ActionOutcome.Failed)
    expect(store.localError).toBe(SshKeyLocalError.PrivateKey)
    expect(stub.requests).toHaveLength(0)
  })

  it('refuses a malformed key or an empty label locally', async () => {
    const store = useSshKeysStore()
    expect(await store.add('1', 'x', 'ssh-rsa AAAA')).toBe(ActionOutcome.Failed)
    expect(store.localError).toBe(SshKeyLocalError.InvalidKey)
    expect(await store.add('1', '  ', KEY)).toBe(ActionOutcome.Failed)
    expect(store.localError).toBe(SshKeyLocalError.LabelRequired)
    expect(stub.requests).toHaveLength(0)
  })

  it('adds a key, trimmed, and appends it', async () => {
    table[`POST ${keysPath('7')}`] = { body: key('new') }
    const store = useSshKeysStore()
    expect(await store.add('7', ' laptop ', KEY)).toBe(ActionOutcome.Succeeded)
    const [sent] = requestsTo(stub.requests, keysPath('7'))
    expect(sent?.method).toBe('POST')
    expect(JSON.parse(sent?.body ?? '')).toEqual({ label: 'laptop', publicKey: KEY })
    expect(store.keys.map(k => k.keyId)).toEqual(['new'])
  })

  it('shows the API sentence and refreshes the list when adding fails', async () => {
    table[`POST ${keysPath('7')}`] = refusal('The hosting server could not be reached.')
    table[`GET ${keysPath('7')}`] = { body: [{ ...key('k'), syncState: SshKeySyncState.InstallFailed }] }
    const store = useSshKeysStore()
    expect(await store.add('7', 'laptop', KEY)).toBe(ActionOutcome.Failed)
    expect(store.addError).toBe('The hosting server could not be reached.')
    expect(store.keys[0]?.syncState).toBe(SshKeySyncState.InstallFailed)
    expect(store.adding).toBe(false)
  })

  it('revokes a key and removes it from the list', async () => {
    table[`GET ${accessPath('7')}`] = { body: access(SshAccessState.Available) }
    table[`GET ${keysPath('7')}`] = { body: [key('a'), key('b')] }
    table[`DELETE ${keysPath('7')}/a`] = { status: 204 }
    const store = useSshKeysStore()
    await store.load('7')
    expect(await store.revoke('7', 'a')).toBe(ActionOutcome.Succeeded)
    expect(requestsTo(stub.requests, `${keysPath('7')}/a`).map(request => request.method)).toEqual(['DELETE'])
    expect(store.keys.map(k => k.keyId)).toEqual(['b'])
    expect(store.revokingId).toBeNull()
  })

  it('keeps the key and the sentence when revoking fails', async () => {
    table[`GET ${keysPath('7')}`] = { body: [key('a')] }
    table[`DELETE ${keysPath('7')}/a`] = refusal('Unavailable')
    const store = useSshKeysStore()
    expect(await store.revoke('7', 'a')).toBe(ActionOutcome.Failed)
    expect(store.revokeError).toBe('Unavailable')
    expect(store.keys).toHaveLength(1)
  })

  it('reports the key limit and clears everything on reset', async () => {
    table[`GET ${accessPath('7')}`] = { body: access(SshAccessState.Available) }
    table[`GET ${keysPath('7')}`] = { body: [key('a'), key('b')] }
    const store = useSshKeysStore()
    await store.load('7')
    expect(store.atLimit).toBe(true)
    store.reset()
    expect(store.access).toBeNull()
    expect(store.keys).toEqual([])
  })
})
