/**
 * Server SSH key management setup: opening the guided dialog from a server row, generating a
 * per-server identity and its hash-verifying installer command, saving with the stored key kept
 * (never read back) while the dialog stays open and confirms the save, and the agent health check:
 * a failure explained in words, and an agent bound to another server.
 */
import { HttpMethod } from '../src/types/http'
import { SshAgentProfile, SshProviderFailure } from '../src/types/sshenums'
import { expect, test } from '@playwright/test'
import { hostServer, hostServerSsh, hostServerSshSetup } from './fixtures/servers'
import type { ServerSshSettingsDto, ServerSshSetupDto } from '../src/types/serverssh'
import { json, stubApi } from './support/stubApi'

/** The settings path of the fixture server. */
const SSH_PATH = `/api/admin/servers/${hostServer.id}/ssh-settings`

/** The setup path of the fixture server. */
const SETUP_PATH = `/api/admin/servers/${hostServer.id}/ssh-setup`

test('generating an identity shows an installer command that verifies the bundle hash', async ({ page }) => {
  const generated: ServerSshSetupDto = {
    ...hostServerSshSetup,
    publicKey: 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b hostpanel-server-1',
    binding: '44444444-4444-4444-8444-444444444444',
  }
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/servers', [hostServer]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Get, SSH_PATH, hostServerSsh),
      json(HttpMethod.Get, SETUP_PATH, hostServerSshSetup),
      json(HttpMethod.Post, `/api/admin/servers/${hostServer.id}/ssh-identity`, generated),
    ],
  })

  await page.goto('/servers')
  await page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`server-ssh-${hostServer.id}`).click()
  await expect(page.getByTestId('server-ssh-install-command')).toHaveCount(0)
  await page.getByTestId('server-ssh-generate').click()

  const command = page.getByTestId('server-ssh-install-command')
  await expect(command).toContainText('sha256sum -c -')
  await expect(command).toContainText(hostServerSshSetup.bundleSha256)
  await expect(command).toContainText(`--binding '${generated.binding}'`)
  await expect(command).not.toContainText('PRIVATE')
})

test('enabling keeps the stored private key and stays in the dialog with a confirmation', async ({ page }) => {
  let stored: ServerSshSettingsDto = { ...hostServerSsh }
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/servers', [hostServer]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      { method: HttpMethod.Get, path: SSH_PATH, respond: () => ({ body: stored }) },
      json(HttpMethod.Get, SETUP_PATH, hostServerSshSetup),
      {
        method: HttpMethod.Put,
        path: SSH_PATH,
        respond: () => {
          stored = { ...stored, isEnabled: true }
          return { body: stored }
        },
      },
    ],
  })

  await page.goto('/servers')
  await page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`server-ssh-${hostServer.id}`).click()
  await expect(page.getByTestId('server-ssh-hostname')).toHaveValue(hostServerSsh.sshHostname)
  await expect(page.getByTestId('server-ssh-private-key')).toHaveValue('')

  await page.getByTestId('server-ssh-enabled').click()
  await page.getByTestId('server-ssh-save').click()

  await expect(page.getByTestId('server-ssh-saved')).toHaveText('Saved')
  await expect.poll(() => api.callsTo(HttpMethod.Put, SSH_PATH).length).toBe(1)
  const [put] = api.callsTo(HttpMethod.Put, SSH_PATH)
  const body = JSON.parse(put?.postData ?? '{}') as Record<string, unknown>
  expect(body.isEnabled).toBe(true)
  expect(body.agentPrivateKey).toBeNull()
  await expect(page.getByTestId('server-ssh-hostname')).toBeVisible()
  await expect(page.getByTestId('servers-feedback')).toHaveText('')
})

test('a failed agent test says why in words', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/servers', [hostServer]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Get, SSH_PATH, { ...hostServerSsh, isHealthy: false }),
      json(HttpMethod.Get, SETUP_PATH, hostServerSshSetup),
      json(HttpMethod.Post, `${SSH_PATH}/test`, { isHealthy: false, agentVersion: null, error: SshProviderFailure.Unreachable, provider: null }),
    ],
  })

  await page.goto('/servers')
  await page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`server-ssh-${hostServer.id}`).click()
  await page.getByTestId('server-ssh-test').click()

  await expect(page.getByTestId('server-ssh-agent-error')).toContainText('the server could not be reached')
  await expect(page.getByTestId('server-ssh-health')).toContainText('Agent unreachable')
})

test('a test against an agent installed for another server explains the mismatch', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/servers', [hostServer]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Get, SSH_PATH, { ...hostServerSsh, isHealthy: false }),
      json(HttpMethod.Get, SETUP_PATH, hostServerSshSetup),
      json(HttpMethod.Post, `${SSH_PATH}/test`, {
        isHealthy: false,
        agentVersion: '1.1.0',
        error: SshProviderFailure.BindingMismatch,
        provider: SshAgentProfile.Cwp,
      }),
    ],
  })

  await page.goto('/servers')
  await page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`server-ssh-${hostServer.id}`).click()
  await page.getByTestId('server-ssh-test').click()

  await expect(page.getByTestId('server-ssh-binding-mismatch')).toBeVisible()
  await expect(page.getByTestId('server-ssh-health')).toContainText('Agent unreachable')
})
