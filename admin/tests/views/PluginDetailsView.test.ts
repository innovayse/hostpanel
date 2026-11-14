/**
 * Mount tests for PluginDetailsView: the page of every installed plugin, built from this server's list. The loading, error
 * (with retry) and not-found states; the registry as optional enrichment (a refusal, a 404 or a switched-off registry only hides
 * the Versions, Licence and Changes tabs and the update action); the Settings tab; an install behind a confirmation, a downgrade
 * behind its own explicit confirmation, an update to the newest version, an uninstall behind the shared removal confirmation that
 * raises the restart banner and returns to the plugins page, a refusal shown as the API's sentence, and the write-only licence key
 * of a paid plugin (save, remove, refusal).
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginDetailsView from '@/views/PluginDetailsView.vue'
import { PluginCapability, PluginErrorCode, PluginPricing, PluginStatus } from '@/types/plugin'
import { callOf, json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { integrationDetail, integrationDto } from '../helpers/integrationFixtures'
import { makeDetails, makeInstalled, makeVersion } from '../helpers/pluginDetails'
import { stubRoutes as stubAllRoutes } from '../helpers/routes'

/** The address of the fixture plugin's details. */
const DETAILS_URL = 'GET /api/admin/plugins/registry/example-pay'

/**
 * Stubs the routes a test names plus empty answers for the lists an install refreshes. Routes match
 * by prefix and the first key wins, so the longest (most specific) key goes first.
 *
 * @param routes - Answers keyed by the start of `METHOD url`.
 * @returns The fetch mock, for asserting the requests made.
 */
const stubRoutes = (routes: Record<string, () => Response>) =>
  stubAllRoutes(Object.fromEntries(
    Object.entries({
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry?': () => json({ items: [], total: 0, pageSize: 20 }),
      'GET /api/admin/plugins/example-pay/slot-widgets': () => json([]),
      'GET /api/admin/plugins': () => json([makeInstalled()]),
      'GET /api/admin/integrations': () => json([]),
      'GET /api/products': () => json([]),
      ...routes,
    }).sort(([a], [b]) => b.length - a.length),
  ))

/** The tabs a test may open. */
type PageTab = 'settings' | 'versions' | 'licence' | 'changes'

/**
 * Mounts the page for the fixture plugin, waits for the load and opens a tab when one is named.
 *
 * @param tab - The tab to switch to; the overview stays on show when omitted.
 * @returns The wrapper and the router.
 */
const mountScreenAt = async (tab?: PageTab) => {
  const screen = await mountRouted(PluginDetailsView, '/plugins/:id', '/plugins/example-pay')
  await flushPromises()
  if (tab !== undefined) {
    await screen.wrapper.get(`[data-testid="plugin-details-tab-${tab}"]`).trigger('click')
  }
  return screen
}

/**
 * Mounts the page for the fixture plugin, waits for the load and opens a tab when one is named.
 *
 * @param tab - The tab to switch to; the overview stays on show when omitted.
 * @returns The wrapper.
 */
const mountPage = async (tab?: PageTab) => (await mountScreenAt(tab)).wrapper

/**
 * The requests of the mock that used a verb.
 *
 * @param mock - The fetch mock.
 * @param method - The HTTP verb.
 * @returns Index of every call with that verb.
 */
const callsWith = (mock: ReturnType<typeof vi.fn>, method: string): number[] =>
  mock.mock.calls.flatMap((_, index) => (callOf(mock, index).method === method ? [index] : []))

describe('PluginDetailsView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the plugin, its links and its versions', async () => {
    stubRoutes({ [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.find('[data-testid="plugin-details-link-documentation"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-version-3.0.0"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-tab-licence"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-details-tab-versions"]').trigger('click')

    expect(wrapper.find('[data-testid="plugin-version-3.0.0"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-version-1.0.0"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-link-documentation"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-licence-card"]').exists()).toBe(false)
  })

  it('shows the skeleton while the details load', async () => {
    stubRoutes({ [DETAILS_URL]: () => json(makeDetails()) })
    const { wrapper } = await mountRouted(PluginDetailsView, '/plugins/:id', '/plugins/example-pay')

    expect(wrapper.find('[data-testid="plugin-details-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-tab-overview"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-links"]').exists()).toBe(true)
    await flushPromises()
    expect(wrapper.find('[data-testid="plugin-details-loading"]').exists()).toBe(false)
  })

  it('shows the installed plugin and the API sentence when only the registry is refused, and loads again from the retry', async () => {
    let calls = 0
    stubRoutes({
      [DETAILS_URL]: () => {
        calls += 1
        return calls === 1 ? problem('The registry is unavailable.', 503) : json(makeDetails())
      },
    })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.get('[data-testid="plugin-details-registry-error"]').text()).toContain('The registry is unavailable.')
    expect(wrapper.find('[data-testid="plugin-details-tab-versions"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-details-registry-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-details-registry-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-tab-versions"]').exists()).toBe(true)
  })

  it('shows the API sentence when the installed list is refused and loads again from the retry', async () => {
    let refused = true
    stubRoutes({
      'GET /api/admin/plugins': () => (refused ? problem('The server is busy.', 503) : json([makeInstalled()])),
      [DETAILS_URL]: () => json(makeDetails()),
    })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="plugin-details-error"]').text()).toContain('The server is busy.')
    expect(wrapper.find('[data-testid="plugin-details-not-found"]').exists()).toBe(false)
    refused = false
    await wrapper.get('[data-testid="plugin-details-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
  })

  it('shows the not-found state for an id that is not installed', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([makeInstalled({ id: 'other' })]), [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="plugin-details-not-found"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-name"]').exists()).toBe(false)
  })

  it('shows a removed plugin that only keeps data as not found', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([makeInstalled({ status: PluginStatus.Removed })]), [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="plugin-details-not-found"]').exists()).toBe(true)
  })

  it('works without registry details when the registry answers 404, with no error and no registry tabs', async () => {
    stubRoutes({ [DETAILS_URL]: () => problem('No such plugin.', 404) })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.find('[data-testid="plugin-details-not-found"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-registry-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-overview"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-tab-versions"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-tab-licence"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-tab-changes"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-uninstall"]').exists()).toBe(true)
  })

  it('works without registry details when the registry is switched off, silently', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json({ type: PluginErrorCode.RegistryDisabled, title: 'Off', status: 409, detail: 'The registry is switched off.' }, 409),
    })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.find('[data-testid="plugin-details-registry-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-tab-versions"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
  })

  it('shows the installed plugin of the list with no registry listing for a built-in one, without uninstall', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([makeInstalled({ isBuiltIn: true })]),
      [DETAILS_URL]: () => problem('No such plugin.', 404),
    })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.find('[data-testid="plugin-details-uninstall"]').exists()).toBe(false)
  })

  it('shows the capabilities and release notes of the installed plugin on the overview', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([makeInstalled({ changelog: 'Faster refunds.', capabilities: [PluginCapability.Endpoints] })]),
      [DETAILS_URL]: () => problem('No such plugin.', 404),
    })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="plugin-details-changelog"]').text()).toBe('Faster refunds.')
    expect(wrapper.find('[data-testid="plugin-details-capability-endpoints"]').exists()).toBe(true)
  })

  it('shows the config form on the Settings tab when the integrations list has the plugin id as a slug', async () => {
    stubRoutes({
      [DETAILS_URL]: () => problem('No such plugin.', 404),
      'GET /api/admin/integrations/example-pay': () => json(integrationDetail({ slug: 'example-pay' })),
      'GET /api/admin/integrations': () => json([integrationDto({ slug: 'example-pay', isPlugin: true })]),
    })
    const wrapper = await mountPage('settings')
    await flushPromises()

    expect(wrapper.find('[data-testid="integration-config-form"]').exists()).toBe(true)
  })

  it('shows no config form on the Settings tab for a plugin without an integration, and the slot widgets of that plugin', async () => {
    const widget = { slotId: 'admin.service.panels', widgetId: 'usage', title: 'Usage', manifestProductIds: [], productIds: [], isBound: false }
    const mock = stubRoutes({
      [DETAILS_URL]: () => problem('No such plugin.', 404),
      'GET /api/admin/plugins/example-pay/slot-widgets': () => json([widget]),
    })
    const wrapper = await mountPage('settings')

    expect(wrapper.find('[data-testid="integration-config-panel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-slot-widgets-example-pay"]').exists()).toBe(true)
    expect(mock.mock.calls.some(([url]) => String(url) === '/api/admin/plugins/example-pay/slot-widgets')).toBe(true)
  })

  it('offers no Settings tab and loads no widgets for a plugin that is not loaded', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([makeInstalled({ isLoaded: false })]),
      [DETAILS_URL]: () => problem('No such plugin.', 404),
    })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="plugin-details-tab-settings"]').exists()).toBe(false)
    expect(mock.mock.calls.some(([url]) => String(url).includes('slot-widgets'))).toBe(false)
  })

  it('links back to the list from the start of the row, with an icon', async () => {
    stubRoutes({ [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage()

    const back = wrapper.get('[data-testid="plugin-details-back"]')
    expect(back.classes()).toContain('self-start')
    expect(back.find('svg').exists()).toBe(true)
  })

  it.each([PluginErrorCode.RegistryLicenceRequired, PluginErrorCode.RegistryLicenceInvalid])(
    'offers the licence key card with the error sentence when the page is gated (%s), and reloads after the key is saved',
    async code => {
      let saved = false
      const mock = stubRoutes({
        [DETAILS_URL]: () => saved
          ? json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true, hasLicenceKey: true }))
          : json({ type: code, title: 'Licence', status: 422, detail: 'A licence key is needed.' }, 422),
        'PUT /api/admin/plugins/registry/example-pay/licence': () => {
          saved = true
          return new Response(null, { status: 204 })
        },
      })
      const wrapper = await mountPage('licence')

      expect(wrapper.get('[data-testid="plugin-details-registry-error"]').text()).toContain('A licence key is needed.')
      expect(wrapper.find('[data-testid="plugin-licence-card"]').exists()).toBe(true)
      await wrapper.get('[data-testid="plugin-licence-input"]').setValue('KEY-123')
      await wrapper.get('[data-testid="plugin-licence-save"]').trigger('click')
      await flushPromises()

      const [put] = callsWith(mock, 'PUT')
      expect(callOf(mock, put as number).body).toEqual({ key: 'KEY-123' })
      expect(wrapper.find('[data-testid="plugin-details-registry-error"]').exists()).toBe(false)
      expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    },
  )

  it('offers no licence key card for an ordinary load failure', async () => {
    stubRoutes({ [DETAILS_URL]: () => problem('The registry is unavailable.', 503) })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="plugin-licence-card"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-tab-licence"]').exists()).toBe(false)
  })

  it('installs a newer version only after the confirmation, without allowing a downgrade', async () => {
    const mock = stubRoutes({
      [DETAILS_URL]: () => json(makeDetails()),
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true }),
    })
    const wrapper = await mountPage('versions')

    await wrapper.get('[data-testid="plugin-version-install-3.0.0"]').trigger('click')
    await flushPromises()
    expect(callsWith(mock, 'POST')).toHaveLength(0)
    expect(wrapper.find('[data-testid="plugin-details-install-dialog"]').exists()).toBe(true)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const [post] = callsWith(mock, 'POST')
    expect(callOf(mock, post as number).body).toEqual({ version: '3.0.0', allowDowngrade: false })
    expect(wrapper.find('[data-testid="plugin-processing-outcome"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(true)
  })

  it('lists the release notes of every version between the installed one and the new one', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({
        latestVersion: '4.0.0',
        versions: [
          makeVersion({ version: '4.0.0', changelog: 'Fourth **release**.' }),
          makeVersion({ version: '3.0.0', changelog: 'New checkout flow.' }),
          makeVersion({ version: '2.5.0' }),
          makeVersion({ version: '2.0.0', changelog: 'Installed notes.' }),
        ],
      })),
    })
    const wrapper = await mountPage('changes')

    const card = wrapper.get('[data-testid="plugin-details-changes"]')
    expect(card.get('[data-testid="plugin-update-changes-heading"]').text()).toBe('What changes from 2.0.0 to 4.0.0')
    expect(card.get('[data-testid="plugin-update-change-4.0.0"] strong').text()).toBe('release')
    expect(card.get('[data-testid="plugin-update-change-3.0.0"]').text()).toContain('New checkout flow.')
    expect(card.get('[data-testid="plugin-update-change-2.5.0"]').text()).toContain('No release notes.')
    expect(card.find('[data-testid="plugin-update-change-2.0.0"]').exists()).toBe(false)
  })

  it('shows no changes card when no update is on offer', async () => {
    stubRoutes({ [DETAILS_URL]: () => json(makeDetails({ updateAvailable: false, latestVersion: '2.0.0' })) })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="plugin-details-tab-changes"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-changes"]').exists()).toBe(false)
  })

  it('offers the Changes tab only while an update is pending, and the Licence tab only for a plugin that needs a key', async () => {
    stubRoutes({ [DETAILS_URL]: () => json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true })) })
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="plugin-details-tab-changes"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-tab-licence"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-changes"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-details-tab-changes"]').trigger('click')

    expect(wrapper.find('[data-testid="plugin-details-changes"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-link-documentation"]').exists()).toBe(false)
  })

  it('repeats the changes in the update confirmation', async () => {
    stubRoutes({ [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage()

    await wrapper.get('[data-testid="plugin-details-update"]').trigger('click')
    await flushPromises()

    const dialog = wrapper.get('[data-testid="plugin-details-install-dialog"]')
    expect(dialog.get('[data-testid="plugin-update-change-3.0.0"]').text()).toContain('Faster refunds.')
  })

  it('updates to the newest version from the header, behind the confirmation', async () => {
    const mock = stubRoutes({
      [DETAILS_URL]: () => json(makeDetails()),
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true, stagedVersion: '3.0.0' }),
    })
    const wrapper = await mountPage()

    await wrapper.get('[data-testid="plugin-details-update"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const [post] = callsWith(mock, 'POST')
    expect(callOf(mock, post as number).body).toEqual({ version: '3.0.0', allowDowngrade: false })
    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('staged')
  })

  it('asks for an explicit downgrade confirmation before installing an older version', async () => {
    const mock = stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({
        versions: [makeVersion({ version: '3.0.0' }), makeVersion({ version: '2.0.0' }), makeVersion({ version: '1.0.0' })],
      })),
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true, stagedVersion: '1.0.0' }),
    })
    const wrapper = await mountPage('versions')

    await wrapper.get('[data-testid="plugin-version-install-1.0.0"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="plugin-details-install-dialog"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-details-downgrade-dialog"]').text()).toContain('older')
    expect(callsWith(mock, 'POST')).toHaveLength(0)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const [post] = callsWith(mock, 'POST')
    expect(callOf(mock, post as number).body).toEqual({ version: '1.0.0', allowDowngrade: true })
  })

  it('installs nothing when the confirmation is dismissed', async () => {
    const mock = stubRoutes({ [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage('versions')

    await wrapper.get('[data-testid="plugin-version-install-3.0.0"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(callsWith(mock, 'POST')).toHaveLength(0)
    expect(wrapper.find('[data-testid="plugin-details-install-dialog"]').exists()).toBe(false)
  })

  it('shows the API sentence when an install is refused', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json(makeDetails()),
      'POST /api/admin/plugins/registry/example-pay/install': () => problem('This version needs a licence key.', 409),
    })
    const wrapper = await mountPage('versions')

    await wrapper.get('[data-testid="plugin-version-install-3.0.0"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('This version needs a licence key.')
  })

  it('uninstalls behind the shared confirmation, raises the restart banner and returns to the plugins page', async () => {
    let removed = false
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json(removed ? [makeInstalled({ status: PluginStatus.Removed })] : [makeInstalled()]),
      [DETAILS_URL]: () => json(makeDetails()),
      'DELETE /api/admin/plugins/example-pay': () => {
        removed = true
        return json({ requiresRestart: true })
      },
    })
    const { wrapper, router } = await mountScreenAt()

    await wrapper.get('[data-testid="plugin-details-uninstall"]').trigger('click')
    await flushPromises()
    expect(callsWith(mock, 'DELETE')).toHaveLength(0)
    expect(wrapper.get('[data-testid="plugin-remove-dialog"]').text()).toContain('stored data are kept')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callsWith(mock, 'DELETE')).toHaveLength(1)
    expect(router.currentRoute.value.path).toBe('/plugins')
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
  })

  it('does not remove anything when the removal confirmation is dismissed', async () => {
    const mock = stubRoutes({ [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage()

    await wrapper.get('[data-testid="plugin-details-uninstall"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(callsWith(mock, 'DELETE')).toHaveLength(0)
    expect(wrapper.find('[data-testid="plugin-remove-dialog"]').exists()).toBe(false)
  })

  it('shows the API sentence when the uninstall is refused', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json(makeDetails()),
      'DELETE /api/admin/plugins/example-pay': () => problem('Plugin is in use.', 409),
    })
    const wrapper = await mountPage()

    await wrapper.get('[data-testid="plugin-details-uninstall"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Plugin is in use.')
  })

  it('shows a yanked version with its reason and replacement', async () => {
    stubRoutes({ [DETAILS_URL]: () => json(makeDetails()) })
    const wrapper = await mountPage('versions')

    expect(wrapper.get('[data-testid="plugin-version-status-1.0.0"]').text()).toBe('Yanked')
    expect(wrapper.get('[data-testid="plugin-version-reason-1.0.0"]').text()).toContain('Charges twice.')
    expect(wrapper.get('[data-testid="plugin-version-replacement-1.0.0"]').text()).toContain('2.0.0')
    expect(wrapper.get('[data-testid="plugin-version-install-1.0.0"]').attributes('disabled')).toBeDefined()
  })

  it('saves the licence key of a paid plugin and shows that a key is saved', async () => {
    let saved = false
    const mock = stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true, hasLicenceKey: saved })),
      'PUT /api/admin/plugins/registry/example-pay/licence': () => {
        saved = true
        return new Response(null, { status: 204 })
      },
    })
    const wrapper = await mountPage('licence')

    expect(wrapper.find('[data-testid="plugin-licence-missing"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-licence-needed"]').exists()).toBe(true)
    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('KEY-123')
    await wrapper.get('[data-testid="plugin-licence-save"]').trigger('click')
    await flushPromises()

    const [put] = callsWith(mock, 'PUT')
    expect(callOf(mock, put as number).body).toEqual({ key: 'KEY-123' })
    expect(wrapper.find('[data-testid="plugin-licence-saved"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-licence-needed"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-details-feedback"]').text()).toContain('licence key is saved')
  })

  it('removes the saved licence key', async () => {
    let saved = true
    const mock = stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true, hasLicenceKey: saved })),
      'DELETE /api/admin/plugins/registry/example-pay/licence': () => {
        saved = false
        return new Response(null, { status: 204 })
      },
    })
    const wrapper = await mountPage('licence')

    await wrapper.get('[data-testid="plugin-licence-remove"]').trigger('click')
    await flushPromises()

    expect(callsWith(mock, 'DELETE')).toHaveLength(1)
    expect(wrapper.find('[data-testid="plugin-licence-missing"]').exists()).toBe(true)
  })

  it('shows the API sentence when the licence key is refused', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true })),
      'PUT /api/admin/plugins/registry/example-pay/licence': () => problem('The key is not valid.', 400),
    })
    const wrapper = await mountPage('licence')

    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('BAD')
    await wrapper.get('[data-testid="plugin-licence-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-details-feedback"]').text()).toContain('The key is not valid.')
  })

  it('empties the key field after a key replaces one that is already saved', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true, hasLicenceKey: true })),
      'PUT /api/admin/plugins/registry/example-pay/licence': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountPage('licence')

    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('KEY-NEW')
    await wrapper.get('[data-testid="plugin-licence-save"]').trigger('click')
    await flushPromises()

    expect((wrapper.get('[data-testid="plugin-licence-input"]').element as HTMLInputElement).value).toBe('')
  })

  it('keeps the typed key when the save is refused', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true, hasLicenceKey: true })),
      'PUT /api/admin/plugins/registry/example-pay/licence': () => problem('The key is not valid.', 400),
    })
    const wrapper = await mountPage('licence')

    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('BAD')
    await wrapper.get('[data-testid="plugin-licence-save"]').trigger('click')
    await flushPromises()

    expect((wrapper.get('[data-testid="plugin-licence-input"]').element as HTMLInputElement).value).toBe('BAD')
  })

  it('empties the key field after the saved key is removed', async () => {
    stubRoutes({
      [DETAILS_URL]: () => json(makeDetails({ pricing: PluginPricing.Paid, requiresLicence: true, hasLicenceKey: true })),
      'DELETE /api/admin/plugins/registry/example-pay/licence': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountPage('licence')

    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('TYPED')
    await wrapper.get('[data-testid="plugin-licence-remove"]').trigger('click')
    await flushPromises()

    expect((wrapper.get('[data-testid="plugin-licence-input"]').element as HTMLInputElement).value).toBe('')
  })
})
