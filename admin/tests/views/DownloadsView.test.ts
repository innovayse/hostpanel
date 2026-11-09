/**
 * Smoke tests for DownloadsView: the category grid and file list, navigating into a category,
 * opening the edit dialog and deleting after confirmation, through the real store and transport.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import DownloadsView from '@/views/DownloadsView.vue'
import { DownloadType } from '@/types/downloaditem'
import { mountView } from '../helpers/mount'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'

/** One top-level category as the endpoint returns it. */
const category = { id: 1, name: 'Drivers', description: 'Hardware drivers', isHidden: false, parentCategoryId: null, downloadCount: 2 }

/** One download as the endpoint returns it. */
const download = {
  id: 9,
  title: 'Setup',
  description: 'Installer',
  type: DownloadType.Zip,
  filename: 'setup.zip',
  categoryId: 1,
  categoryName: 'Drivers',
  downloadCount: 12,
  clientsOnly: true,
  productDownload: false,
  isHidden: false,
  createdAt: '2026-10-01T00:00:00Z',
}

/** The two collections the page loads. */
const loadRoutes = [
  { path: '/downloads/categories', body: [category] },
  { path: '/downloads', body: [download] },
]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('DownloadsView', () => {
  it('shows the category and the file with its badges', async () => {
    stubPathRoutes(loadRoutes)

    const wrapper = await mountView(DownloadsView)

    expect(wrapper.get('[data-testid="downloads-category-1"]').text()).toContain('Drivers')
    expect(wrapper.get('[data-testid="downloads-download-9"]').text()).toContain('Setup')
    expect(wrapper.get('[data-testid="downloads-download-9"]').text()).toContain('Clients Only')
  })

  it('goes into a category, loads its files and adds a breadcrumb', async () => {
    const mock = stubPathRoutes(loadRoutes)
    const wrapper = await mountView(DownloadsView)

    await wrapper.get('[data-testid="downloads-category-1-open"]').trigger('click')
    await flushPromises()

    const urls = mock.mock.calls.map((_, index) => callOf(mock, index).url)
    expect(urls).toContain('/api/downloads?categoryId=1')
    expect(wrapper.get('[data-testid="downloads-breadcrumbs"]').text()).toContain('Drivers')
  })

  it('opens the category edit dialog from the row actions menu without drilling into the category', async () => {
    const mock = stubPathRoutes(loadRoutes)
    const wrapper = await mountView(DownloadsView)
    const loads = mock.mock.calls.length

    // Edit and delete sit behind the row's own menu trigger.
    await wrapper.get('[data-testid="downloads-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="downloads-category-1-edit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="downloads-edit-category-name"]').exists()).toBe(true)
    expect(mock.mock.calls.length).toBe(loads)
    expect(wrapper.get('[data-testid="downloads-breadcrumbs"]').text()).not.toContain('Drivers')
  })

  it('opens the edit dialog for a file', async () => {
    stubPathRoutes(loadRoutes)
    const wrapper = await mountView(DownloadsView)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)

    await wrapper.get('[data-testid="downloads-download-9-open"]').trigger('click')

    expect((wrapper.get('[data-testid="downloads-edit-download-title"]').element as HTMLInputElement).value).toBe('Setup')
  })

  it('deletes a file after confirmation', async () => {
    const mock = stubPathRoutes(loadRoutes)
    const wrapper = await mountView(DownloadsView)

    await wrapper.get('[data-testid="downloads-download-9-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const removal = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.method === 'DELETE')
    expect(removal).toMatchObject({ url: '/api/downloads/9' })
  })

  it('shows the API sentence when loading is refused', async () => {
    stubFetch(problem('Not allowed.', 403))

    const wrapper = await mountView(DownloadsView)

    expect(wrapper.get('[data-testid="downloads-error"]').text()).toBe('Not allowed.')
  })

  /**
   * Stubs the two loads and the answer of the file's delete.
   *
   * @param answer - The answer of `DELETE /api/downloads/9`.
   * @returns The fetch mock.
   */
  const stubDelete = (answer: () => Response): ReturnType<typeof vi.fn> =>
    stubRoutes({
      'GET /api/downloads/categories': () => json([category]),
      'GET /api/downloads': () => json([download]),
      'DELETE /api/downloads/9': answer,
    })

  it('reports a successful delete under the header', async () => {
    stubDelete(() => new Response(null, { status: 204 }))
    const wrapper = await mountView(DownloadsView)

    await wrapper.get('[data-testid="downloads-download-9-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="downloads-feedback"]').text()).toBe('Deleted.')
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubDelete(() => problem('The file is in use.', 409))
    const wrapper = await mountView(DownloadsView)

    await wrapper.get('[data-testid="downloads-download-9-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="downloads-feedback"]').text()).toBe('The file is in use.')
    expect(wrapper.find('[data-testid="downloads-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubDelete(() => new Response(null, { status: 204 }))
    const wrapper = await mountView(DownloadsView)

    await wrapper.get('[data-testid="downloads-download-9-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('reloads from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403), problem('Not allowed.', 403), json([category]), json([download]))
    const wrapper = await mountView(DownloadsView)

    await wrapper.get('[data-testid="downloads-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="downloads-error"]').exists()).toBe(false)
  })
})
